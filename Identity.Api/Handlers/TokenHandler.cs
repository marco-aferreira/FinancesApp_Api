using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Identity.Api.Handlers;

public class TokenHandler
{
    private readonly IConfiguration _configuration;
    private static string PublicKey { get; set; } = "";
    public TokenHandler(IConfiguration configuration)
    {
        _configuration = configuration;
        PublicKey = _configuration["PublicKeyPath"] ?? throw new InvalidOperationException("Public key not found in configuration.");
    }
    public ClaimsPrincipal? DecodeToken(string token)
    {
        TokenValidationParameters validationParams = GetTokenValidationParameters();

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParams, out SecurityToken validatedToken);
            return principal;
        }
        catch (SecurityTokenExpiredException)
        {
            Console.WriteLine("Token has expired.");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            Console.WriteLine($"Token invalid: {ex.Message}");
            return null;
        }
    }

    public TokenValidationParameters GetTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(LoadPublicKey()),
            ValidateIssuer = true,
            ValidIssuer = _configuration.GetValue<string>("JwtInfo:Issuer"),
            ValidateAudience = true,
            ValidAudience = _configuration.GetValue<string>("JwtInfo:Audience"),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    public RSA LoadPublicKey()
    {
        var rsa = RSA.Create();
        string pem = File.ReadAllText(PublicKey);
        rsa.ImportFromPem(pem);
        return rsa;
    }
}
