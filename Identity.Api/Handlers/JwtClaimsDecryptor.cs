using Identity.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;


namespace Identity.Api.Handlers;

public class JwtClaimsDecryptor(IConfiguration configuration)
{
    public DecryptedJwt Decrypt(ClaimsPrincipal user, string? requiredTokenType = null)
    {
        var tokenType = user.FindFirst("token_type")?.Value ?? "";

        if (requiredTokenType is not null && tokenType != requiredTokenType)
            return new DecryptedJwt(JwtDecryptionStatus.InvalidTokenType, tokenType);

        var encryptedUserId = user.FindFirst("userid_enc")?.Value;
        if (string.IsNullOrEmpty(encryptedUserId))
            return new DecryptedJwt(JwtDecryptionStatus.MissingUserIdentity, tokenType);

        var encryptionKey = configuration["ClaimEncryptionKey"]
            ?? throw new InvalidOperationException("ClaimEncryptionKey not found in configuration.");

        if (!TryDecrypt(encryptedUserId, encryptionKey, out var decryptedUserId)
            || !Guid.TryParse(decryptedUserId, out var userId))
            return new DecryptedJwt(JwtDecryptionStatus.InvalidUserId, tokenType);

        return new DecryptedJwt(JwtDecryptionStatus.Valid, tokenType)
        {
            UserId = userId,
            Login = DecryptLogin(user, encryptionKey),
            AccountIds = DecryptAccountIds(user, encryptionKey)
        };
    }

    private static string DecryptLogin(ClaimsPrincipal user, string encryptionKey)
    {
        var encryptedLogin = user.FindFirst("sub_enc")?.Value;

        if (string.IsNullOrEmpty(encryptedLogin)
            || !TryDecrypt(encryptedLogin, encryptionKey, out var login))
            return "";

        return login;
    }

    private static List<Guid> DecryptAccountIds(ClaimsPrincipal user, string encryptionKey)
    {
        var encryptedAccountIds = user.FindFirst("account_ids_enc")?.Value;

        if (string.IsNullOrEmpty(encryptedAccountIds)
            || !TryDecrypt(encryptedAccountIds, encryptionKey, out var accountIdsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(accountIdsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool TryDecrypt(string cipherText,
                                   string encryptionKey, 
                                   out string plainText)
    {
        try
        {
            plainText = ClaimEncryption.Decrypt(cipherText, encryptionKey);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            plainText = "";
            return false;
        }
    }
}

public static class DecryptedJwtExtensions
{
    public static IActionResult ToUnauthorizedResult(this DecryptedJwt jwt) => jwt.Status switch
    {
        JwtDecryptionStatus.InvalidTokenType => new UnauthorizedObjectResult("Full authentication required."),
        JwtDecryptionStatus.MissingUserIdentity => new UnauthorizedObjectResult("Missing user identity."),
        JwtDecryptionStatus.InvalidUserId => new UnauthorizedObjectResult("Invalid user identity."),
        _ => new UnauthorizedResult()
    };
}
