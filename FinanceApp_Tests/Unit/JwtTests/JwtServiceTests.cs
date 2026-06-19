using System.Net;
using FinancesApp_Api.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace FinancesApp_Tests.Unit.JwtTests;

/// <summary>
/// Unit tests for the phantom-token seam in <see cref="JwtService"/>:
/// minting an opaque session reference (/session) and exchanging it for a JWT (/exchange).
/// The HTTP boundary to Identity.Api is faked with a stub message handler, so no
/// Identity.Api / Redis / SQL is required.
/// </summary>
public class JwtServiceTests
{
    private const string InternalSecret = "test-internal-secret";

    private static (JwtService service, StubHandler handler) CreateService(HttpStatusCode status, string responseBody)
    {
        var handler = new StubHandler(status, responseBody);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5002")
        };

        var config = Substitute.For<IConfiguration>();
        config["InternalSecret"].Returns(InternalSecret);

        return (new JwtService(httpClient, config), handler);
    }

    [Fact]
    public async Task ExchangeReferenceForJwt_Posts_To_Exchange_And_Returns_Jwt()
    {
        const string phantomJwt = "header.payload.signature";
        var (service, handler) = CreateService(HttpStatusCode.OK, phantomJwt);

        var result = await service.ExchangeReferenceForJwt("opaque-ref-123");

        result.Should().Be(phantomJwt);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/exchange");
        handler.LastRequest.Headers.GetValues("X-Internal-Secret").Should().ContainSingle()
            .Which.Should().Be(InternalSecret);
        handler.LastBody.Should().Contain("opaque-ref-123");
    }

    [Fact]
    public async Task ExchangeReferenceForJwt_Throws_When_Identity_Rejects_The_Reference()
    {
        // A revoked / expired / unknown reference makes /exchange return 401.
        // The shim relies on this throwing so it can leave the request unauthenticated.
        var (service, _) = CreateService(HttpStatusCode.Unauthorized, "");

        var act = async () => await service.ExchangeReferenceForJwt("bad-ref");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetUserSessionToken_Posts_To_Session_And_Returns_Bare_Reference()
    {
        const string opaqueRef = "Zm9vYmFyYmF6";
        var (service, handler) = CreateService(HttpStatusCode.OK, opaqueRef);

        var request = new GenerateFullJwtRequest
        {
            UserId = Guid.NewGuid(),
            Login = "john@example.com",
            AccountIds = [Guid.NewGuid()],
            CustomClaims = new Dictionary<string, object> { { "role", "user" }, { "2fa_verified", true } }
        };

        var result = await service.GetUserSessionToken(request);

        result.Should().Be(opaqueRef);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/session");
        handler.LastRequest.Headers.GetValues("X-Internal-Secret").Should().ContainSingle()
            .Which.Should().Be(InternalSecret);
        handler.LastBody.Should().Contain("john@example.com");
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                     CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}
