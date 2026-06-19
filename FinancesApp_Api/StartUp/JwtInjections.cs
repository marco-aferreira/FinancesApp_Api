    using FinancesApp_Api.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace FinancesApp_Api.StartUp;

public static class JwtInjections
{
    /// <summary>
    /// Authorization policy requiring a post-2FA token (token_type = "full").
    /// Partial tokens (password-only, pre-TOTP) are rejected.
    /// </summary>
    public const string FullTokenPolicy = "FullToken";

    public static IServiceCollection AddJwtServices(this IServiceCollection services, IConfigurationManager configuration)
    {

        services.AddSingleton<JwtHandler>();
        services.AddSingleton<JwtClaimsDecryptor>();

        services.AddHttpClient<JwtService>(client =>
        {
            client.BaseAddress = new Uri(configuration["IdentityApi:BaseAddress"] ?? "http://localhost:5002");
        });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {

            var jwtHandler = new JwtHandler(configuration);

            options.TokenValidationParameters = jwtHandler.GetTokenValidationParameters();

            options.Events = new JwtBearerEvents
            {
                // Phantom-token shim (stand-in for the AWS Lambda authorizer):
                // the X-Access-Token cookie holds an opaque session reference, not a JWT.
                // Exchange it for a short-lived phantom JWT via Identity.Api /exchange,
                // caching the result in Redis so we don't hit /exchange on every request.
                OnMessageReceived = async context =>
                {
                    // Pre-2FA flow sends a real partial JWT as a Bearer header — leave it alone.
                    if (context.Request.Headers.ContainsKey("Authorization"))
                        return;

                    if (!context.Request.Cookies.TryGetValue("X-Access-Token", out var reference)
                        || string.IsNullOrWhiteSpace(reference))
                        return;

                    context.Token = await ResolvePhantomToken(context, reference);
                },
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Console.WriteLine($"Token validated for: {context.Principal?.Identity?.Name}");
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(FullTokenPolicy, policy =>
                policy.RequireClaim("token_type", "full"));
        });

        return services;
    }

    /// <summary>
    /// Exchanges the opaque session reference for a phantom JWT via Identity.Api /exchange,
    /// caching the result in Redis (short TTL) to avoid an exchange round-trip per request.
    /// Returns null when the reference is expired/revoked/unknown so the request stays unauthenticated.
    /// </summary>
    private static async Task<string?> ResolvePhantomToken(MessageReceivedContext context, string reference)
    {
        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IDistributedCache>();
        var jwtService = services.GetRequiredService<JwtService>();
        var ct = context.HttpContext.RequestAborted;

        var cacheKey = "phantom:" + Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(reference)));

        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cached))
            return cached;

        try
        {
            var jwt = await jwtService.ExchangeReferenceForJwt(reference, ct);

            await cache.SetStringAsync(cacheKey, jwt, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            }, ct);

            return jwt;
        }
        catch (HttpRequestException)
        {
            // Expired / revoked / unknown reference — leave context.Token unset (request → 401).
            return null;
        }
    }
}
