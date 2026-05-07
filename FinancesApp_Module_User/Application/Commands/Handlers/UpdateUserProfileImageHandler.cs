using FinancesApp_CQRS.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinancesApp_Module_User.Application.Commands.Handlers;

public class UpdateUserProfileImageHandler(
    IEventStore eventStore,
    ILogger<UpdateUserProfileImageHandler> logger) : ICommandHandler<UpdateUserProfileImage, bool>
{
    public async Task<bool> Handle(UpdateUserProfileImage command, CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await eventStore.Load(command.UserId, token: cancellationToken);
            var user = new Domain.User();
            user.RebuildFromEvents(events);

            user.UpdateProfileImage(command.S3Key);

            await eventStore.Append(command.UserId, user.GetUncommittedEvents(), user.CurrentVersion, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update profile image for user {UserId}", command.UserId);
            return false;
        }
    }
}
