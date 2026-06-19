using System.Net;
using System.Net.Http.Headers;
using FinancesApp_CQRS.Queries;
using FinancesApp_Module_Account.Domain;
using FluentAssertions;
using NSubstitute;

namespace FinancesApp_Tests.Integration.Auth;

/// <summary>
/// End-to-end tests for the phantom-token authentication path: an opaque session reference
/// in the X-Access-Token cookie is exchanged for a JWT by the in-process shim, validated by
/// JwtBearer, and the decrypted user identity reaches the controller.
///
/// A fresh factory per test keeps the mutable stubs (exchange responder, call counts) isolated.
/// </summary>
public class PhantomTokenAuthE2ETests
{
    private const string AccountsUrl = "/api/v1/accounts";

    [Fact]
    public async Task Valid_Reference_Is_Exchanged_And_Request_Is_Authenticated()
    {
        using var factory = new PhantomTokenWebFactory();
        var userId = Guid.NewGuid();

        factory.ExchangeStub.RespondWith(HttpStatusCode.OK, factory.MintFullJwt(userId));
        factory.AccountsHandler
            .Handle(Arg.Any<GetAccounts>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Account>());

        var response = await SendWithReference(factory, "opaque-ref-abc");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.ExchangeStub.ExchangeCallCount.Should().Be(1);

        // The userid_enc claim from the exchanged JWT was decrypted and flowed to the query.
        await factory.AccountsHandler.Received(1)
            .Handle(Arg.Is<GetAccounts>(q => q.UserId == userId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repeated_Requests_Hit_The_Cache_And_Exchange_Only_Once()
    {
        using var factory = new PhantomTokenWebFactory();
        var userId = Guid.NewGuid();

        factory.ExchangeStub.RespondWith(HttpStatusCode.OK, factory.MintFullJwt(userId));
        factory.AccountsHandler
            .Handle(Arg.Any<GetAccounts>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Account>());

        var first = await SendWithReference(factory, "same-ref");
        var second = await SendWithReference(factory, "same-ref");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.ExchangeStub.ExchangeCallCount.Should().Be(1); // second resolved from Redis-substitute cache
    }

    [Fact]
    public async Task Rejected_Reference_Returns_401()
    {
        using var factory = new PhantomTokenWebFactory();

        // Expired / revoked / unknown reference => Identity.Api /exchange returns 401.
        factory.ExchangeStub.RespondWith(HttpStatusCode.Unauthorized, "");

        var response = await SendWithReference(factory, "revoked-ref");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.ExchangeStub.ExchangeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Missing_Cookie_Returns_401_Without_Calling_Exchange()
    {
        using var factory = new PhantomTokenWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(AccountsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.ExchangeStub.ExchangeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Authorization_Header_Bypasses_The_Exchange_Shim()
    {
        using var factory = new PhantomTokenWebFactory();
        var userId = Guid.NewGuid();

        factory.AccountsHandler
            .Handle(Arg.Any<GetAccounts>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Account>());

        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, AccountsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.MintFullJwt(userId));
        // A cookie is present too, but the Authorization header must win and skip the exchange.
        request.Headers.Add("Cookie", "X-Access-Token=should-be-ignored");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.ExchangeStub.ExchangeCallCount.Should().Be(0);
    }

    private static async Task<HttpResponseMessage> SendWithReference(PhantomTokenWebFactory factory, string reference)
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, AccountsUrl);
        request.Headers.Add("Cookie", $"X-Access-Token={reference}");
        return await client.SendAsync(request);
    }
}
