using Identity.Api.Contracts;
using Identity.Api.Filters;
using Identity.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace Identity.Api.Controllers;

[ApiController]
public class IdentityController(IConfiguration configuration,
                                IDynamoService dynamoService,
                                IDistributedCache cache) : ControllerBase
{
    private static readonly TimeSpan PartialTokenLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FullTokenLifetime = TimeSpan.FromMinutes(30);

    private static string ActiveRefCacheKey(Guid userId) => $"session:user:{userId}";

    [HttpPost("token")]
    public IActionResult GenerateToken([FromBody] TokenGenerationRequest request)
                                       => Ok(CreateJwt(request));

    [ServiceFilter(typeof(InternalSecretFilter))]
    [HttpPost("session")]
    public async Task<IActionResult> CreateUserSessionToken([FromBody] CreateSessionRequest request,
                                                                       CancellationToken token = default)
    {
        var cacheKey = ActiveRefCacheKey(request.UserId);

        // Reuse the user's active ref if one is still cached (TTL == session lifetime, so a hit
        // means it hasn't expired). On a miss (expired/evicted) we fall through and mint a new one.
        var existingRef = await cache.GetStringAsync(cacheKey, token);
        if (!string.IsNullOrEmpty(existingRef))
            return Ok(existingRef);

        var sessionRefBytes = RandomNumberGenerator.GetBytes(32);

        var hashed = SHA256.HashData(sessionRefBytes);

        var referenceObj = new SessionTokenReference(Convert.ToBase64String(hashed),
                                                     request.UserId,
                                                     request.Email,
                                                     request.UserAccountIds,
                                                     request.CustomClaims,
                                                     DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                                     DateTimeOffset.UtcNow.Add(FullTokenLifetime).ToUnixTimeSeconds(),
                                                     false);

        await dynamoService.SaveUserSessionReference(referenceObj, token);

        var sessionRef = Base64Url.EncodeToString(sessionRefBytes);

        await cache.SetStringAsync(cacheKey, sessionRef,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = FullTokenLifetime }, token);

        return Ok(sessionRef);
    }

    [ServiceFilter(typeof(InternalSecretFilter))]
    [HttpPost("exchange")]
    public async Task<IActionResult> ExchangeAccessTokenForJwt([FromBody] ExchangeRequest request,
                                                               CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(request.AccessToken))
            return BadRequest();

        var buffer = new byte[request.AccessToken.Length];
        if (!Base64Url.TryDecodeFromChars(request.AccessToken, buffer, out int bytesWritten))
            return BadRequest();

        var hash = Convert.ToBase64String(SHA256.HashData(buffer.AsSpan(0, bytesWritten)));

        var session = await dynamoService.GetByRefHash(hash, token);

        if (session.UserId == Guid.Empty
            || session.Revoked
            || session.ExpiresAt <= DateTimeOffset.UtcNow)
            return Unauthorized();

        var jwtToken = CreateJwt(new TokenGenerationRequest
        {
            UserId = session.UserId,
            Login = session.Login,
            TokenType = TokenType.Full,
            AccountIds = session.AccountIds,
            CustomClaims = session.CustomClaims
        });

        return Ok(jwtToken);
    }

    [ServiceFilter(typeof(InternalSecretFilter))]
    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeAccessToken([FromBody] RevokeRequest request,
                                                       CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(request.AccessToken))
            return BadRequest();

        var buffer = new byte[request.AccessToken.Length];
        if (!Base64Url.TryDecodeFromChars(request.AccessToken, buffer, out int bytesWritten))
            return BadRequest();

        var hash = Convert.ToBase64String(SHA256.HashData(buffer.AsSpan(0, bytesWritten)));

        var userId = await dynamoService.RevokeByRefHash(hash, token);

        // Drop the cached active ref so /session stops handing the revoked ref back.
        if (userId is not null)
            await cache.RemoveAsync(ActiveRefCacheKey(userId.Value), token);

        return NoContent();
    }

    private string CreateJwt(TokenGenerationRequest request)
    {
        var encryptionKey = configuration["ClaimEncryptionKey"]
            ?? throw new InvalidOperationException("ClaimEncryptionKey not found in configuration.");

        var isPartial = request.TokenType == TokenType.Partial;
        var lifetime = isPartial ? PartialTokenLifetime : FullTokenLifetime;

        var claims = BuildClaims(request, encryptionKey, isPartial);

        var rsa = new KeyUtils(configuration).LoadPrivateKey();
        var rsaKey = new RsaSecurityKey(rsa);
        var signingCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(lifetime),
            Issuer = "https://FinancesApp.com",
            Audience = "https://FinancesAppCustomers.com",
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    private static List<Claim> BuildClaims(TokenGenerationRequest request, string encryptionKey, bool isPartial)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new("token_type", isPartial ? "partial" : "full"),
            new("sub_enc", ClaimEncryption.Encrypt(request.Login, encryptionKey)),
            new("userid_enc", ClaimEncryption.Encrypt(request.UserId.ToString(), encryptionKey))
        };

        if (!isPartial && request.AccountIds.Count > 0)
        {
            var accountIdsJson = JsonSerializer.Serialize(request.AccountIds);
            claims.Add(new Claim("account_ids_enc", ClaimEncryption.Encrypt(accountIdsJson, encryptionKey)));
        }

        if (isPartial)        
            claims.Add(new Claim("2fa_pending", "true", ClaimValueTypes.Boolean));
        
        foreach (var claimPair in request.CustomClaims)
        {
            var jsonElement = (JsonElement)claimPair.Value;

            var valueType = jsonElement.ValueKind switch
            {
                JsonValueKind.True => ClaimValueTypes.Boolean,
                JsonValueKind.False => ClaimValueTypes.Boolean,
                JsonValueKind.Number => ClaimValueTypes.Double,
                _ => ClaimValueTypes.String
            };

            var claim = new Claim(claimPair.Key, claimPair.Value.ToString()!, valueType);
            claims.Add(claim);
        }

        return claims;
    }
}
