using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Credentials.Application.Queries;
using FinancesApp_Module_Credentials.Domain;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace FinancesApp_Module_Credentials.Application.Commands.Handlers;
public class LogoutUserHandler(IQueryHandler<GetActiveUserTotp, UserCredentialsTotp?> getActiveTotpHandler,
                               ICommandHandler<InvalidateTotpCredential, bool> invalidateTotpHandler,
                               ILogger<LogoutUserHandler> logger) : ICommandHandler<LogoutUser, bool>
{
    private static readonly Counter LogoutCounter = Metrics
        .CreateCounter("user_logout_total", "Total number of user logouts.");

    public async Task<bool> Handle(LogoutUser command, CancellationToken cancellationToken = default)
    {
        try
        {
            var activeTotp = await getActiveTotpHandler.Handle(
                new GetActiveUserTotp { UserId = command.UserId }, cancellationToken);

            if (activeTotp is not null)
            {
                var invalidated = await invalidateTotpHandler.Handle(
                    new InvalidateTotpCredential(activeTotp.Id), cancellationToken);

                logger.LogInformation("Logout — TOTP invalidation for UserId {UserId}: {Result}",
                    command.UserId, invalidated);
            }
            else
            {
                logger.LogInformation("Logout — no active TOTP for UserId {UserId}", command.UserId);
            }

            LogoutCounter.Inc();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging out UserId {UserId}", command.UserId);
            return false;
        }
    }
}
