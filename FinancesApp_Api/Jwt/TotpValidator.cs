using System.Security.Claims;
using FinancesApp_Api.Contracts.Requests.CredentialsRequests;
using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Credentials.Application.Commands;
using FinancesApp_Module_Credentials.Application.Queries;
using FinancesApp_Module_Credentials.Domain;

namespace FinancesApp_Api.Jwt;

public class TotpValidator(TotpService totpService,
                           IQueryHandler<GetActiveUserTotp, UserCredentialsTotp?> getActiveTotpHandler,
                           ICommandHandler<InvalidateTotpCredential, bool> invalidateTotpHandler,
                           JwtClaimsDecryptor jwtDecryptor,
                           ILogger<TotpValidator> logger)
{
    public async Task<TotpValidationResult> Validate(VerifyTwoFactorRequest request,
                                                      ClaimsPrincipal user,
                                                      CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(request.TotpCode) || request.TotpCode.Length != 6)
            return new TotpValidationResult(TotpValidationStatus.InvalidCodeFormat);

        var jwt = jwtDecryptor.Decrypt(user, requiredTokenType: "partial");
        if (!jwt.IsValid)
            return new TotpValidationResult(MapDecryptionStatus(jwt.Status));

        var userGuid = jwt.UserId;

        var activeTotp = await getActiveTotpHandler.Handle(
            new GetActiveUserTotp { UserId = userGuid }, token);

        if (activeTotp is null)
            return new TotpValidationResult(TotpValidationStatus.NoActiveTotp);

        if (DateTimeOffset.UtcNow > activeTotp.InvalidAt)
        {
            await invalidateTotpHandler.Handle(new InvalidateTotpCredential(activeTotp.Id), token);
            return new TotpValidationResult(TotpValidationStatus.TotpExpired);
        }

        var codeValid = totpService.VerifyCode(activeTotp.SecurityCode, request.TotpCode);
        logger.LogInformation(
            "TOTP verify — TotpId: {TotpId}, UserId: {UserId}, SecurityCode: {SecurityCode}, " +
            "CreatedAt: {CreatedAt}, InvalidAt: {InvalidAt}, ServerUtcNow: {UtcNow}, Result: {Result}",
            activeTotp.Id, userGuid, activeTotp.SecurityCode,
            activeTotp.CreatedAt, activeTotp.InvalidAt, DateTimeOffset.UtcNow, codeValid);

        if (!codeValid)
            return new TotpValidationResult(TotpValidationStatus.InvalidCode);

        return new TotpValidationResult(TotpValidationStatus.Valid, userGuid, activeTotp.Id);
    }

    private static TotpValidationStatus MapDecryptionStatus(JwtDecryptionStatus status) => status switch
    {
        JwtDecryptionStatus.InvalidTokenType => TotpValidationStatus.InvalidTokenType,
        JwtDecryptionStatus.MissingUserIdentity => TotpValidationStatus.MissingUserIdentity,
        _ => TotpValidationStatus.InvalidUserId
    };
}
