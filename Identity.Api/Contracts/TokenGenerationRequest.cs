namespace Identity.Api.Contracts;

public enum TokenType { Partial, Full }

public class TokenGenerationRequest
{
    public Guid UserId { get; set; }

    public string Login { get; set; } = "";

    public TokenType TokenType { get; set; } = TokenType.Full;

    public List<Guid> AccountIds { get; set; } = [];

    public Dictionary<string, object> CustomClaims { get; set; } = [];
}
