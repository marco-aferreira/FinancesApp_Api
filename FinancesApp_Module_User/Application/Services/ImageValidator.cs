namespace FinancesApp_Module_User.Application.Services;

public class ImageValidator : IImageValidator
{
    private const long MaxBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedTypes =
        ["image/jpeg", "image/png", "image/webp"];

    public (bool IsValid, string? Error) Validate(byte[] data, string contentType)
    {
        if (data.Length == 0)
            return (false, "Image data is empty.");

        if (data.Length > MaxBytes)
            return (false, $"Image exceeds 2 MB limit ({data.Length / 1024} KB received).");

        var type = contentType.ToLowerInvariant();

        if (!AllowedTypes.Contains(type))
            return (false, $"Content type '{contentType}' is not allowed. Accepted: jpeg, png, webp.");

        if (!MatchesMagicBytes(data, type))
            return (false, "File content does not match declared content type.");

        return (true, null);
    }

    private static bool MatchesMagicBytes(byte[] data, string contentType) => contentType switch
    {
        "image/jpeg" =>
            data.Length >= 3 &&
            data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,

        "image/png" =>
            data.Length >= 8 &&
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A,

        // RIFF at bytes 0-3, WEBP at bytes 8-11
        "image/webp" =>
            data.Length >= 12 &&
            data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50,

        _ => false
    };
}
