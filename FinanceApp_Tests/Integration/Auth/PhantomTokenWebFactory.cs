using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinancesApp_Api.Jwt;
using FinancesApp_CQRS.Interfaces;
using FinancesApp_CQRS.Queries;
using FinancesApp_Module_Account.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace FinancesApp_Tests.Integration.Auth;

/// <summary>
/// Boots FinancesApp_Api in-process for end-to-end testing of the phantom-token shim
/// (JwtInjections.OnMessageReceived -> JwtService.ExchangeReferenceForJwt -> JWT validation).
///
/// Identity.Api, Redis and SQL are NOT required:
///  - Identity.Api /exchange is faked via <see cref="ExchangeStub"/> on JwtService's HttpClient.
///  - Redis is replaced with an in-memory distributed cache (so cache hit/miss is real).
///  - The GetAccounts query handler is stubbed, so no database is touched.
///  - Hosted services (OutboxProcessor, image upload) are removed.
///
/// JWTs minted here are signed with an RSA key whose public half is written to the
/// PublicKeyPath the API validates against, so the tokens validate for real.
/// </summary>
public class PhantomTokenWebFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "https://FinancesApp.com";
    public const string Audience = "https://FinancesAppCustomers.com";

    public string ClaimEncryptionKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public ExchangeStub ExchangeStub { get; } = new();
    public IQueryHandler<GetAccounts, IReadOnlyList<Account>> AccountsHandler { get; } =
        Substitute.For<IQueryHandler<GetAccounts, IReadOnlyList<Account>>>();

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _publicKeyPath =
        Path.Combine(Path.GetTempPath(), $"phantom-e2e-{Guid.NewGuid():N}.pem");

    public PhantomTokenWebFactory()
    {
        File.WriteAllText(_publicKeyPath, _rsa.ExportSubjectPublicKeyInfoPem());
    }

    /// <summary>Mints a full, RSA-signed JWT the API will accept — simulates the /exchange output.</summary>
    public string MintFullJwt(Guid userId, DateTime? expires = null)
    {
        var claims = new List<Claim>
        {
            new("token_type", "full"),
            new("userid_enc", EncryptClaim(userId.ToString()))
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>Encrypts a claim value using the same AES-CBC + IV-prefix scheme as ClaimEncryption.Decrypt.</summary>
    public string EncryptClaim(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(ClaimEncryptionKey);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var data = Encoding.UTF8.GetBytes(plaintext);
        var cipher = encryptor.TransformFinalBlock(data, 0, data.Length);

        var combined = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, combined, aes.IV.Length, cipher.Length);
        return Convert.ToBase64String(combined);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DbConnection"] = "Server=(local);Database=Test;Trusted_Connection=True;TrustServerCertificate=True",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["PublicKeyPath"] = _publicKeyPath,
                ["JwtInfo:Issuer"] = Issuer,
                ["JwtInfo:Audience"] = Audience,
                ["ClaimEncryptionKey"] = ClaimEncryptionKey,
                ["InternalSecret"] = "e2e-internal-secret",
                ["IdentityApi:BaseAddress"] = "http://identity.local",
                ["Aws:Region"] = "us-east-1",
                ["Aws:S3:BucketName"] = "test-bucket",
                ["Aws:DynamoDb:TableName"] = "test-table"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // No background services — keeps the outbox processor off SQL.
            services.RemoveAll<IHostedService>();

            // In-memory distributed cache instead of Redis (cache hit/miss still exercised).
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            // Fake Identity.Api /exchange by swapping JwtService's primary HTTP handler.
            services.AddHttpClient<JwtService>()
                    .ConfigurePrimaryHttpMessageHandler(() => ExchangeStub);

            // Stub the accounts read path so no database is required.
            services.RemoveAll<IQueryHandler<GetAccounts, IReadOnlyList<Account>>>();
            services.AddSingleton(AccountsHandler);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _rsa.Dispose();
        try { File.Delete(_publicKeyPath); } catch { /* best effort */ }
    }
}

/// <summary>
/// Stands in for Identity.Api. Records /exchange calls (to assert caching) and returns a
/// configurable response so tests can simulate success / rejection of a session reference.
/// </summary>
public sealed class ExchangeStub : HttpMessageHandler
{
    private (HttpStatusCode Status, string Body) _response = (HttpStatusCode.OK, "");

    public int ExchangeCallCount { get; private set; }
    public string? LastExchangeBody { get; private set; }

    public void RespondWith(HttpStatusCode status, string body) => _response = (status, body);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                 CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("/exchange", StringComparison.Ordinal))
        {
            ExchangeCallCount++;
            if (request.Content is not null)
                LastExchangeBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(_response.Status)
        {
            Content = new StringContent(_response.Body)
        };
    }
}
