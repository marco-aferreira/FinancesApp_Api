using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Credentials.Application.Repositories;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace FinancesApp_Module_Credentials.Application.Commands.Handlers;
public class DeleteUserCredentialsHandler(IEventStore eventStore,
                                          IUserCredentialsReadRepository readRepository,
                                          ILogger<DeleteUserCredentialsHandler> logger) : ICommandHandler<DeleteUserCredentials, bool>
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly IUserCredentialsReadRepository _readRepository = readRepository;
    private readonly ILogger<DeleteUserCredentialsHandler> _logger = logger;

    private static readonly Counter CredentialsDeleted = Metrics
        .CreateCounter("credentials_total_Delete", "Total number of credentials deleted.");

    private static readonly Histogram CredentialsDeleteDuration = Metrics
        .CreateHistogram("credentials_Delete_duration_seconds", "Credentials deletion processing time.",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.1, count: 10)
            });

    public async Task<bool> Handle(DeleteUserCredentials command, CancellationToken cancellationToken = default)
    {
        using (CredentialsDeleteDuration.NewTimer())
        {
            _logger.LogInformation("Attempting to delete credentials for UserID: {UserId}", command.UserId);

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

                credentials.Delete();

                await _eventStore.Append(existing.Id, credentials.GetUncommittedEvents(), credentials.CurrentVersion, cancellationToken);

                CredentialsDeleted.Inc();

                _logger.LogInformation("Credentials deleted successfully - UserID: {UserId}", command.UserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting credentials for UserID {UserId}", command.UserId);
                return false;
            }
        }
    }
}
