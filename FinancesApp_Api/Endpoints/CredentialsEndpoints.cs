namespace FinancesApp_Api.Endpoints;
public static class CredentialsEndpoints
{
    private const string Base = "api/v{version:apiVersion}/credentials";

    public const string GetByUserId = $"{Base}/user/{{userId}}";
    public const string GetByLogin = $"{Base}/login/{{login}}";
    public const string CreateCredentials = $"{Base}";
    public const string UpdateCredentials = $"{Base}/{{userId}}";
    public const string DeleteCredentials = $"{Base}/{{userId}}";
    public const string Login = $"{Base}/login";
    public const string VerifyTwoFactor = $"{Base}/verify-2fa";
    public const string Logout = $"{Base}/logout";
    public const string RebuildProjection = $"{Base}/rebuild-projection/{{userId}}";
}
