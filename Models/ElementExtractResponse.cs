using System.Text.Json.Serialization;

namespace HtmlElementsApi.Models;

public class ElementExtractResponse
{
    [JsonPropertyName("is_error")]
    public int IsError { get; set; }

    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("elements_count")]
    public int ElementsCount { get; set; }

    [JsonPropertyName("emails_count")]
    public int EmailsCount { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("decrypted_plain_text")]
    public string DecryptedPlainText { get; set; } = string.Empty;

    [JsonPropertyName("elements_attr_list")]
    public List<string> ElementsAttrList { get; set; } = [];

    [JsonPropertyName("emails_list")]
    public List<string> EmailsList { get; set; } = [];

    public static ElementExtractResponse Success() => new() { IsError = 0 };

    public static ElementExtractResponse Failure(string errorCode, string errorMessage) =>
        new()
        {
            IsError = 1,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };
}
