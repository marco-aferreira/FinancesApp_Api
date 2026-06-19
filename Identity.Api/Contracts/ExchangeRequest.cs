namespace Identity.Api.Contracts;

/// <summary>
/// Body for POST /exchange — the opaque session ref (Base64Url of the raw ref bytes)
/// the gateway swaps for a freshly minted full JWT.
/// </summary>
public record ExchangeRequest(string AccessToken);
