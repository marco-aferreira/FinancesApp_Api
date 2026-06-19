namespace Identity.Api.Contracts;

/// <summary>
/// Body for POST /revoke — the opaque session ref (Base64Url of the raw ref bytes)
/// whose DynamoDB row should be marked revoked so it can no longer be exchanged.
/// </summary>
public record RevokeRequest(string AccessToken);
