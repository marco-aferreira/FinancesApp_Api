using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Identity.Api.Filters;

/// <summary>
/// Gates internal-only endpoints (/session, later /exchange, /revoke) behind a shared secret
/// passed in the X-Internal-Secret header. Only callers that know the secret (FinancesApp_Api,
/// the gateway Lambda) get through. Temporary measure until private networking is in place.
/// </summary>
public class InternalSecretFilter(IConfiguration configuration) : IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Internal-Secret";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var expected = configuration["InternalSecret"]
            ?? throw new InvalidOperationException("InternalSecret not found in configuration.");

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !FixedTimeEquals(provided.ToString(), expected))
        {
            context.Result = new UnauthorizedResult();
        }

        return Task.CompletedTask;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
