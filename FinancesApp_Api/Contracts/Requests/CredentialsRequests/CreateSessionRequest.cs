namespace FinancesApp_Api.Contracts.Requests.CredentialsRequests;

public class CreateSessionRequest
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public List<Guid> UserAccountIds { get; set; } = [];
    public Dictionary<string, object> CustomClaims { get; set; } = [];
}