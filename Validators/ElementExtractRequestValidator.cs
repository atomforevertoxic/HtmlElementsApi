using FluentValidation;
using HtmlElementsApi.Models;

namespace HtmlElementsApi.Validators;

public class ElementExtractRequestValidator : AbstractValidator<ElementExtractRequest>
{
    public ElementExtractRequestValidator()
    {
        RuleFor(x => x.Selector)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("selector is required")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .NotEmpty()
            .WithMessage("selector must not be empty")
            .WithErrorCode(ErrorCodes.EmptySelector);

        RuleFor(x => x.Attribute)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("attribute is required")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .NotEmpty()
            .WithMessage("attribute must not be empty")
            .WithErrorCode(ErrorCodes.EmptyAttribute);

        RuleFor(x => x.UrlB64)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("url_b64 is required")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .NotEmpty()
            .WithMessage("url_b64 must not be empty")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .Must(IsValidBase64)
            .WithMessage("url_b64 is not a valid Base64 string")
            .WithErrorCode(ErrorCodes.InvalidUrlBase64);

        RuleFor(x => x.EncryptedTextBytesB64)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("encrypted_text_bytes_b64 is required")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .NotEmpty()
            .WithMessage("encrypted_text_bytes_b64 must not be empty")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .Must(IsValidBase64)
            .WithMessage("encrypted_text_bytes_b64 is not a valid Base64 string")
            .WithErrorCode(ErrorCodes.InvalidCiphertextBase64)
            .Must(BeBlockAligned)
            .WithMessage("encrypted_text_bytes_b64 must decode to a multiple of 16 bytes")
            .WithErrorCode(ErrorCodes.InvalidCiphertextBase64);

        RuleFor(x => x.KeyBytesB64)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("key_bytes_b64 is required")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .NotEmpty()
            .WithMessage("key_bytes_b64 must not be empty")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .Must(IsValidBase64)
            .WithMessage("key_bytes_b64 is not a valid Base64 string")
            .WithErrorCode(ErrorCodes.InvalidKeyBase64)
            .Must(BeSupportedAesKeyLength)
            .WithMessage("key_bytes_b64 must decode to a 16, 24 or 32 byte AES key")
            .WithErrorCode(ErrorCodes.InvalidKeyBase64);

        RuleFor(x => x.PageB64)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("page_b64 is required")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .NotEmpty()
            .WithMessage("page_b64 must not be empty")
            .WithErrorCode(ErrorCodes.MissingParameter)
            .Must(IsValidBase64)
            .WithMessage("page_b64 is not a valid Base64 string")
            .WithErrorCode(ErrorCodes.InvalidPageBase64);
    }

    private static bool IsValidBase64(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool BeBlockAligned(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        return Convert.FromBase64String(value).Length % 16 == 0;
    }

    private static bool BeSupportedAesKeyLength(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        var length = Convert.FromBase64String(value).Length;
        return length is 16 or 24 or 32;
    }
}
