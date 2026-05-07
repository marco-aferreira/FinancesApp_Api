using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using FinancesApp_Api.Contracts.Requests.CredentialsRequests;
using FinancesApp_Api.Contracts.Responses.CredentialsResponses;
using FinancesApp_Api.Endpoints;
using FinancesApp_Api.Jwt;
using FinancesApp_Api.Mapper;
using FinancesApp_Api.StartUp;
using FinancesApp_CQRS.Interfaces;
using FinancesApp_CQRS.Queries;
using FinancesApp_Module_Account.Domain;
using FinancesApp_Module_Credentials.Application.Commands;
using FinancesApp_Module_Credentials.Application.Queries;
using FinancesApp_Module_Credentials.Domain;
using FinancesApp_Module_User.Application.Queries;
using FinancesApp_Module_User.Application.Services;
using FinancesApp_Module_User.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FinancesApp_Module_User.Application.Queries.Handlers;

namespace FinancesApp_Api.Controllers;

[ApiController]
[ApiVersion(ApiVersions.V1)]
[ApiVersion(ApiVersions.V1_1)]
public class UserCredentialsController(IQueryHandler<GetUserCredentialsByUserId, UserCredentials> getCredentialsByUserIdHandler,
                                       IQueryHandler<GetUserCredentialsByLogin, UserCredentials> getCredentialsByLoginHandler,
                                       IQueryHandler<GetUserByEmail, User> getUserByEmailHandler,
                                       IQueryHandler<GetUserById, User> getUserByIdHandler,
                                       IQueryHandler<GetAccounts, IReadOnlyList<Account>> getAccountsHandler,
                                       IQueryHandler<GetActiveUserTotp, UserCredentialsTotp?> getActiveTotpHandler,
                                       ICommandHandler<RegisterUserCredentials, Guid> createCredentialsHandler,
                                       ICommandHandler<UpdateUserCredentials, bool> updateCredentialsHandler,
                                       ICommandHandler<DeleteUserCredentials, bool> deleteCredentialsHandler,
                                       ICommandHandler<TotpCredentialCreated, bool> createTotpHandler,
                                       ICommandHandler<InvalidateTotpCredential, bool> invalidateTotpHandler,
                                       ICommandHandler<RebuildCredentialsProjection, bool> rebuildProjectionHandler,
                                       TotpService totpService,
                                       TotpValidator totpValidator,
                                       IS3ImageService s3ImageService) : ControllerBase
{

    [HttpGet(CredentialsEndpoints.GetByUserId)]
    public async Task<IActionResult> GetByUserId([FromRoute] string userId, CancellationToken token = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return BadRequest("Invalid Id");

        var query = new GetUserCredentialsByUserId
        {
            UserId = userGuid
        };

        var result = await getCredentialsByUserIdHandler.Handle(query, token);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet(CredentialsEndpoints.GetByLogin)]
    public async Task<IActionResult> GetByLogin([FromRoute] string login, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(login))
            return BadRequest("Login cannot be empty");

        var query = new GetUserCredentialsByLogin {
            Login = login
        };

        var result = await getCredentialsByLoginHandler.Handle(query, token);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [EnableRateLimiting(RateLimitingInjections.AuthPolicy)]
    [HttpPost(CredentialsEndpoints.Login)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request,
                                           CancellationToken token = default)
    {
        JwtService jwtService = new();

        var query = new GetUserCredentialsByLogin
        {
            Login = request.Login,
            Password = request.PlainPassword
        };

        var credentials = await getCredentialsByLoginHandler.Handle(query, token);

        if (credentials is null || credentials.Id == Guid.Empty)
            return Unauthorized();

        var existingTotp = await getActiveTotpHandler.Handle(
            new GetActiveUserTotp { UserId = credentials.UserId }, token);
        if (existingTotp is not null)
            await invalidateTotpHandler.Handle(new InvalidateTotpCredential(existingTotp.Id), token);

        var totpResult = totpService.GenerateSecret(credentials.Email);

        var totpAggregate = new UserCredentialsTotp(credentials.UserId, totpResult.Base32Secret);
        var totpCommand = new TotpCredentialCreated(totpAggregate);
        var stored = await createTotpHandler.Handle(totpCommand, token);

        if (!stored)
            return StatusCode(500, "Failed to generate 2FA credentials");

        var partialToken = await jwtService.GeneratePartialToken(credentials, token);

        return Ok(new LoginResponse(Token: partialToken,
                                    QrCodeImage: $"data:image/png;base64,{totpResult.QrCodeBase64}")
        );
    }
    
    [Authorize]
    [EnableRateLimiting(RateLimitingInjections.VerifyTotpPolicy)]
    [HttpPost(CredentialsEndpoints.VerifyTwoFactor)]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request,
                                                     CancellationToken token = default)
    {
        var validation = await totpValidator.Validate(request, User, token);

        if (!validation.IsValid)
            return MapValidationResult(validation);

        await invalidateTotpHandler.Handle(new InvalidateTotpCredential(validation.TotpId), token);

        var credentialsQuery = new GetUserCredentialsByUserId { UserId = validation.UserId };
        var credentials = await getCredentialsByUserIdHandler.Handle(credentialsQuery, token);

        if (credentials is null || credentials.Id == Guid.Empty)
            return NotFound("User credentials not found");

        var accountsQuery = new GetAccounts { UserId = validation.UserId };
        var accounts = await getAccountsHandler.Handle(accountsQuery, token);

        var userAccountIds = accounts
            .Where(a => a.Status == AccountStatus.Active)
            .Select(a => a.Id)
            .ToList();

        var userQuery = new GetUserById { UserId = credentials.UserId};
        var user = await getUserByIdHandler.Handle(userQuery, token);

        string? profileImageUrl = null;

        if (!string.IsNullOrEmpty(user?.ProfileImage))
            profileImageUrl = await s3ImageService.GeneratePresignedUrlAsync(user.ProfileImage, token);
        
        string fullToken = await GetUserFullAccessToken(validation, credentials, userAccountIds, token);

        return Ok(new { Token = fullToken, TokenType = "full", ProfileImageUrl = profileImageUrl });
    }

   

    [HttpPost(CredentialsEndpoints.CreateCredentials)]
    public async Task<IActionResult> CreateCredentials([FromBody] CreateCredentialsRequest request,
                                                        CancellationToken token = default)
    {
        var userQuery = new GetUserByEmail
        {
            Email = request.Email
        };

        var user = await getUserByEmailHandler.Handle(userQuery, token);

        if (user == null)
            return NotFound();

        var command = new RegisterUserCredentials(request.MapToUserCredentials(user.Id));

        var result = await createCredentialsHandler.Handle(command, token);

        if (result == Guid.Empty)
            return BadRequest("Failed to create credentials");

        return Ok(result);
    }

    [HttpPut(CredentialsEndpoints.UpdateCredentials)]
    public async Task<IActionResult> UpdateCredentials([FromBody] UpdateCredentialsRequest request,
                                                       CancellationToken token = default)
    {
        if (!Guid.TryParse(request.UserId, out var userGuid))
            return BadRequest("Invalid Id");

        var command = new UpdateUserCredentials(userGuid, request.NewPlainPassword);
        var result = await updateCredentialsHandler.Handle(command, token);

        if (!result)
            return BadRequest("Failed to update credentials");

        return Ok("Credentials updated successfully");
    }

    [HttpDelete(CredentialsEndpoints.DeleteCredentials)]
    public async Task<IActionResult> DeleteCredentials([FromRoute] string userId, CancellationToken token = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return BadRequest("Invalid Id");

        var command = new DeleteUserCredentials(userGuid);
        var result = await deleteCredentialsHandler.Handle(command, token);

        if (!result)
            return BadRequest("Failed to delete credentials");

        return Ok("Credentials deleted successfully");
    }

    [HttpPost(CredentialsEndpoints.RebuildProjection)]
    public async Task<IActionResult> RebuildProjection([FromRoute] string userId, CancellationToken token = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return BadRequest("Invalid Id");

        var command = new RebuildCredentialsProjection(userGuid);
        var result = await rebuildProjectionHandler.Handle(command, token);

        if (!result)
            return BadRequest("Failed to rebuild projection. Check if events exist for this user.");

        return Ok($"Projection rebuilt for user {userGuid}");
    }

    private static async Task<string> GetUserFullAccessToken(TotpValidationResult validation,
                                                             UserCredentials credentials,
                                                             List<Guid> userAccountIds,
                                                             CancellationToken token)
    {
        JwtService jwtService = new();

        return await jwtService.GenerateFullToken(new GenerateFullJwtRequest
        {
            UserId = validation.UserId,
            Login = credentials.Email,
            AccountIds = userAccountIds,
            CustomClaims = new Dictionary<string, object>
            {
                { "role", "user" },
                { "2fa_verified", true }
            }
        }, token);
    }

    private static IActionResult MapValidationResult(TotpValidationResult result) => result.Status switch
    {
        TotpValidationStatus.InvalidCodeFormat => new BadRequestObjectResult("Invalid TOTP code format. Must be 6 digits."),
        TotpValidationStatus.InvalidTokenType => new UnauthorizedObjectResult("Only partial tokens can be used for 2FA verification."),
        TotpValidationStatus.MissingUserIdentity => new UnauthorizedObjectResult("Invalid token: missing user identity."),
        TotpValidationStatus.InvalidUserId => new BadRequestObjectResult("Invalid UserId in token."),
        TotpValidationStatus.NoActiveTotp => new UnauthorizedObjectResult("No active TOTP found. Please login again to generate a new code."),
        TotpValidationStatus.TotpExpired => new UnauthorizedObjectResult("TOTP code has expired. Please login again."),
        TotpValidationStatus.InvalidCode => new UnauthorizedObjectResult("Invalid TOTP code."),
        _ => new StatusCodeResult(500)
    };
}
