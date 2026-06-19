namespace Identity.Api.Contracts;

/// <summary>
/// Result of resolving an opaque session ref against DynamoDB.
/// Holds the stored claim material plus session metadata used to gate the exchange.
/// </summary>
public class SessionLookup
{
    public Guid UserId { get; set; }
    public string Login { get; set; } = "";
    public List<Guid> AccountIds { get; set; } = [];
    public Dictionary<string, object> CustomClaims { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
