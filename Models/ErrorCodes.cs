namespace HtmlElementsApi.Models;

public static class ErrorCodes
{
    public const string MissingParameter = "MISSING_PARAMETER";
    public const string EmptySelector = "EMPTY_SELECTOR";
    public const string EmptyAttribute = "EMPTY_ATTRIBUTE";
    public const string InvalidUrlBase64 = "INVALID_URL_BASE64";
    public const string InvalidPageBase64 = "INVALID_PAGE_BASE64";
    public const string InvalidKeyBase64 = "INVALID_KEY_BASE64";
    public const string InvalidCiphertextBase64 = "INVALID_CIPHERTEXT_BASE64";
    public const string CssSelectorError = "CSS_SELECTOR_ERROR";
    public const string AesDecryptError = "AES_DECRYPT_ERROR";
    public const string DbError = "DB_ERROR";
    public const string InternalError = "INTERNAL_ERROR";
}
