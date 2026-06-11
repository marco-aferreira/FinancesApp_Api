namespace FinancesApp_Api.Jwt;

public enum JwtDecryptionStatus
{
    Valid,
    InvalidTokenType,
    MissingUserIdentity,
    InvalidUserId
}

public record DecryptedJwt(JwtDecryptionStatus Status, string TokenType = "")
{
    public bool IsValid => Status == JwtDecryptionStatus.Valid;
    public Guid UserId { get; init; }
    public string Login { get; init; } = "";
    public IReadOnlyList<Guid> AccountIds { get; init; } = [];
}
