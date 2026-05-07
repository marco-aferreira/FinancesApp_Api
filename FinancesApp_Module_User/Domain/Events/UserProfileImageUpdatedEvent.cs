using FinancesApp_CQRS.Interfaces;

namespace FinancesApp_Module_User.Domain.Events;

public record UserProfileImageUpdatedEvent(Guid EventId,
                                           DateTimeOffset Timestamp,
                                           Guid UserId,
                                           string S3Key) : IDomainEvent;
