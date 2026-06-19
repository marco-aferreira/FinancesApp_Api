using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Credentials.Application.Repositories;
using FinancesApp_Module_Credentials.Domain;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace FinancesApp_Module_Credentials.Application.Queries.Handlers;
public class GetUserCredentialsByLoginHandler(IUserCredentialsReadRepository credentialsRepository,
                                            IEventStore eventStore,
                                            ILogger<GetUserCredentialsByLoginHandler> logger) : IQueryHandler<GetUserCredentialsByLogin, UserCredentials>
{
    private readonly IUserCredentialsReadRepository _credentialsRepository = credentialsRepository;
    private readonly IEventStore _eventStore = eventStore;
    private readonly ILogger<GetUserCredentialsByLoginHandler> _logger = logger;

    private static readonly Counter GetCredentialsByLoginCounter = Metrics
        .CreateCounter("credentials_total_GetByLogin", "Total number of credentials retrieved by login.");

    private static readonly Histogram GetCredentialsByLoginDuration = Metrics
        .CreateHistogram("credentials_GetByLogin_duration_seconds", "Credentials retrieved by login duration.",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.1, count: 10)
            });

    public async Task<UserCredentials> Handle(GetUserCredentialsByLogin query, CancellationToken cancellationToken = default)
    {
        using (GetCredentialsByLoginDuration.NewTimer())
        {
            try
            {
                var result = await _credentialsRepository.GetByLoginAsync(query.Login, token: cancellationToken);

                if (result.Id == Guid.Empty)
                    return result;

                // When a password is supplied the caller is authenticating — verify it
                // against the event-sourced aggregate. The hash never leaves this handler.
                if (!string.IsNullOrEmpty(query.Password)
                    && !await VerifyPassword(result.Id, query.Password, cancellationToken))
                    return new UserCredentials();

                GetCredentialsByLoginCounter.Inc();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching credentials for Login {Login}", query.Login);
                return new UserCredentials();
            }
        }
    }

    private async Task<bool> VerifyPassword(Guid credentialsId, string plainPassword, CancellationToken token)
    {
        var events = await _eventStore.Load(credentialsId, token: token);

        if (events.Count == 0)
            return false;

        var credentials = new UserCredentials();
        credentials.RebuildFromEvents(events);

        return !credentials.IsDeleted && credentials.VerifyPassword(plainPassword);
    }
}
