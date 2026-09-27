using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Dapper;
using HtmlElementsApi.Models;
using Npgsql;

namespace HtmlElementsApi.Services;

public interface IElementsService
{
    Task<ElementExtractResponse> ProcessAsync(ElementExtractRequest request, CancellationToken cancellationToken);
}

public class ElementsService : IElementsService
{
    private const string InsertSql =
        "INSERT INTO elements (attribute_value, element_html) VALUES (@AttributeValue, @ElementHtml)";

    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private readonly NpgsqlDataSource _dataSource;

    public ElementsService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<ElementExtractResponse> ProcessAsync(
        ElementExtractRequest request,
        CancellationToken cancellationToken)
    {
        var response = ElementExtractResponse.Success();

        try
        {
            if (!TryDecodeBase64Utf8(request.UrlB64, out var url, out var urlError))
            {
                return Fail(response, ErrorCodes.InvalidUrlBase64, urlError!);
            }

            response.Url = url!;

            if (!TryDecodeBase64Utf8(request.PageB64, out var page, out var pageError))
            {
                return Fail(response, ErrorCodes.InvalidPageBase64, pageError!);
            }

            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(page!, cancellationToken);

            AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> found;
            try
            {
                found = document.QuerySelectorAll(request.Selector!);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Fail(response, ErrorCodes.CssSelectorError, ex.Message);
            }

            response.ElementsCount = found.Length;
            var rows = new List<ElementRow>(found.Length);
            foreach (var element in found)
            {
                var value = element.GetAttribute(request.Attribute!) ?? string.Empty;
                response.ElementsAttrList.Add(value);
                rows.Add(new ElementRow { AttributeValue = value, ElementHtml = element.OuterHtml });
            }

            foreach (Match match in EmailRegex.Matches(page!))
            {
                response.EmailsList.Add(match.Value);
            }

            response.EmailsCount = response.EmailsList.Count;

            try
            {
                var key = Convert.FromBase64String(request.KeyBytesB64 ?? string.Empty);
                var cipherText = Convert.FromBase64String(request.EncryptedTextBytesB64 ?? string.Empty);

                using var aes = Aes.Create();
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                using var decryptor = aes.CreateDecryptor();
                using var input = new MemoryStream(cipherText);
                await using var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
                using var reader = new StreamReader(crypto, StrictUtf8, detectEncodingFromByteOrderMarks: false);
                response.DecryptedPlainText = await reader.ReadToEndAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Fail(response, ErrorCodes.AesDecryptError, ex.Message);
            }

            if (rows.Count > 0)
            {
                try
                {
                    await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
                    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                    await connection.ExecuteAsync(
                        new CommandDefinition(InsertSql, rows, transaction: transaction, cancellationToken: cancellationToken));
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Fail(response, ErrorCodes.DbError, ex.Message);
                }
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(response, ErrorCodes.InternalError, ex.Message);
        }
    }

    private static ElementExtractResponse Fail(
        ElementExtractResponse response,
        string errorCode,
        string errorMessage)
    {
        response.IsError = 1;
        response.ErrorCode = errorCode;
        response.ErrorMessage = errorMessage;
        return response;
    }

    private static bool TryDecodeBase64Utf8(string? value, out string? text, out string? error)
    {
        text = null;
        error = null;

        try
        {
            var bytes = Convert.FromBase64String(value ?? string.Empty);
            text = StrictUtf8.GetString(bytes);
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (DecoderFallbackException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed class ElementRow
    {
        public string AttributeValue { get; init; } = string.Empty;

        public string ElementHtml { get; init; } = string.Empty;
    }
}
