using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Credentials.Application.Repositories;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace FinancesApp_Module_Credentials.Application.Commands.Handlers;
public class UpdateUserCredentialsHandler(IEventStore eventStore,
                                          IUserCredentialsReadRepository readRepository,
                                          ILogger<UpdateUserCredentialsHandler> logger) : ICommandHandler<UpdateUserCredentials, bool>
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly IUserCredentialsReadRepository _readRepository = readRepository;
    private readonly ILogger<UpdateUserCredentialsHandler> _logger = logger;

    private static readonly Counter CredentialsUpdated = Metrics
        .CreateCounter("credentials_total_Update", "Total number of credentials updated.");

    private static readonly Histogram CredentialsUpdateDuration = Metrics
        .CreateHistogram("credentials_Update_duration_seconds", "Credentials update processing time.",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.1, count: 10)
            });

    public async Task<bool> Handle(UpdateUserCredentials command, CancellationToken cancellationToken = default)
    {
        using (CredentialsUpdateDuration.NewTimer())
        {
            _logger.LogInformation(
                "Updating password - UserID: {UserId}",
                command.UserId);

            try
            {
                // The credentials aggregate has its own stream — resolve its Id from the read model first.
                var existing = await _readRepository.GetByUserIdAsync(command.UserId, token: cancellationToken);
                if (existing.Id == Guid.Empty)
                    return false;

                var events = await _eventStore.Load(existing.Id, token: cancellationToken);
                if (events.Count == 0)
                    return false;

                var credentials = new Domain.UserCredentials();
                credentials.RebuildFromEvents(events);

                credentials.ChangePassword(command.NewPlainPassword);

                await _eventStore.Append(existing.Id, credentials.GetUncommittedEvents(), credentials.CurrentVersion, cancellationToken);

                CredentialsUpdated.Inc();

                _logger.LogInformation(
                    "Password updated successfully - UserID: {UserId}",
                    command.UserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating password for UserID {UserId}", command.UserId);
                return false;
            }
        }
    }
}
