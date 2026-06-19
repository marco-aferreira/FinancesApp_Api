using FinancesApp_Api.Contracts.Requests.CredentialsRequests;
using FinancesApp_Module_Credentials.Domain;
using System.Text.Json;

namespace FinancesApp_Api.Jwt;

public class JwtService(HttpClient httpClient, IConfiguration configuration)
{
    private const string TokenEndpoint = "/token";
    private const string SessionReferenceEndpoint = "/session";
    private const string ExchangeEndpoint = "/exchange";
    private const string RevokeEndpoint = "/revoke";

    public async Task<string> GeneratePartialToken(UserCredentials credentials,
                                                   CancellationToken token = default)
    {
        var request = new
        {
            credentials.UserId,
            Login = credentials.Email,
            TokenType = (int)TokenType.Partial,
            AccountIds = Array.Empty<Guid>(),
            CustomClaims = new Dictionary<string, object>
            {
                { "role", "user" }
            }
        };

        return await MakeIdentityApiPostRequest(TokenEndpoint,
                                                request,
                                                token);
    }

    public async Task<string> GetUserSessionToken(GenerateFullJwtRequest fullRequest,
                                                  CancellationToken token = default)
    {
        var request = new CreateSessionRequest
        {
            UserId = fullRequest.UserId,
            Email = fullRequest.Login,
            UserAccountIds = fullRequest.AccountIds,
            CustomClaims = fullRequest.CustomClaims
        };

        // /session returns the opaque reference string directly — store it as-is in the cookie.
        return await MakeIdentityApiPostRequest(SessionReferenceEndpoint,
                                                request,
                                                token);
    }

    /// <summary>
    /// Phantom-token exchange: swaps the opaque session reference for a short-lived JWT.
    /// Stand-in for the AWS Lambda authorizer until the gateway is deployed.
    /// </summary>
    public async Task<string> ExchangeReferenceForJwt(string reference,
                                                       CancellationToken token = default)
        => await MakeIdentityApiPostRequest(ExchangeEndpoint,
                                            new { AccessToken = reference },
                                            token);

    /// <summary>
    /// Revokes the opaque session reference at Identity.Api so it can no longer be exchanged.
    /// Idempotent — an unknown/already-revoked ref is a no-op (204).
    /// </summary>
    public async Task RevokeReference(string reference,
                                      CancellationToken token = default)
        => await MakeIdentityApiPostRequest(RevokeEndpoint,
                                            new { AccessToken = reference },
                                            token);
    private async Task<string> MakeIdentityApiPostRequest(string endpoint, object request, CancellationToken token)
    {
        var expected = configuration["InternalSecret"]
            ?? throw new InvalidOperationException("InternalSecret not found in configuration.");
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.Add("X-Internal-Secret", expected);

        var response = await httpClient.SendAsync(httpRequest, token);
        
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Identity API POST {endpoint} : {(int)response.StatusCode}. Body: {await response.Content.ReadAsStringAsync(token)}");

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(token);
    }
}
