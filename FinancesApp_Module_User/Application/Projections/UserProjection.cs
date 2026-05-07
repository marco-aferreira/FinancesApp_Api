using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_User.Application.Repositories;
using FinancesApp_Module_User.Domain;
using FinancesApp_Module_User.Domain.Events;

namespace FinancesApp_Module_User.Application.Projections;

public class UserProjection(IUserRepository userRepository, IProjectionCheckpoint checkpoint) :
                            IEventHandler<UserCreatedEvent>,
                            IEventHandler<UserUpdatedEvent>,
                            IEventHandler<UserDeletedEvent>,
                            IEventHandler<UserProfileImageUpdatedEvent>
{
    public async Task HandleAsync(UserCreatedEvent evt, CancellationToken token = default)
    {
        if (!await checkpoint.TryClaimAsync(evt.EventId, token)) return;
        await userRepository.CreateUserAsync(new User(
            evt.Id, evt.Name, evt.Email, evt.RegisteredAt,
            evt.Timestamp, evt.DateOfBirth, evt.ProfileImage), token: token);
    }

    public async Task HandleAsync(UserUpdatedEvent evt, CancellationToken token = default)
    {
        if (!await checkpoint.TryClaimAsync(evt.EventId, token)) return;
        await userRepository.UpdateUserAsync(new User(
            evt.Id, evt.Name, evt.Email, evt.DateOfBirth, evt.ProfileImage), token: token);
    }

    public async Task HandleAsync(UserDeletedEvent evt, CancellationToken token = default)
    {
        if (!await checkpoint.TryClaimAsync(evt.EventId, token)) return;
        await userRepository.DeleteUserAsync(evt.Id, token: token);
    }

    public async Task HandleAsync(UserProfileImageUpdatedEvent evt, CancellationToken token = default)
    {
        if (!await checkpoint.TryClaimAsync(evt.EventId, token)) return;
        await userRepository.UpdateProfileImageAsync(evt.UserId, evt.S3Key, token: token);
    }
}
