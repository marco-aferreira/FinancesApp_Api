namespace Identity.Api.Contracts;

public record SessionTokenReference(
    string RefHash,                          // partition key
    Guid UserId,
    string Login,
    List<Guid> AccountIds,                   // JSON / SS in Dynamo
    Dictionary<string, object> CustomClaims, // JSON in Dynamo
    long CreatedAt,                          // unix epoch seconds
    long ExpiresAt,                          // unix epoch seconds (TTL attribute)
    bool Revoked
);
