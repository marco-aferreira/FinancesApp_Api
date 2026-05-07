using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_User.Application.Services;
using Microsoft.Extensions.Logging;
using Prometheus;
using System.Threading.Channels;

namespace FinancesApp_Module_User.Application.Commands.Handlers;

public class CreateUserHandler(IEventStore eventStore,
                               ChannelWriter<ImageUploadJob> imageChannel,
                               ILogger<CreateUserHandler> logger) : ICommandHandler<CreateUser, Guid>
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly ChannelWriter<ImageUploadJob> _imageChannel = imageChannel;
    private readonly ILogger<CreateUserHandler> _logger = logger;

    private static readonly Counter UsersCreated = Metrics
        .CreateCounter("user_total_Create", "Total number of users created.");

    private static readonly Histogram UserCreationDuration = Metrics
        .CreateHistogram("user_Create_duration_seconds", "User creation processing time.",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.1, count: 10)
            });

    public async Task<Guid> Handle(CreateUser command, CancellationToken cancellationToken = default)
    {
        using (UserCreationDuration.NewTimer())
        {
            var user = command.User;

            _logger.LogInformation(
                "Creating user - ID: {UserId}, Name: {Name}, Email: {Email}, DateOfBirth: {DateOfBirth}, RegisteredAt: {RegisteredAt}",
                user.Id, user.Name, user.Email, user.DateOfBirth, user.RegisteredAt);

            try
            {
                await _eventStore.Append(user.Id, user.GetUncommittedEvents(), user.CurrentVersion, cancellationToken);

                UsersCreated.Inc();

                if (command.ImageData is not null && command.ContentType is not null)
                    _imageChannel.TryWrite(new ImageUploadJob(user.Id, command.ImageData, command.ContentType));

                _logger.LogInformation(
                    "User created successfully - ID: {UserId}, Name: {Name}, Email: {Email}, RegisteredAt: {RegisteredAt}, ProfileImage: {ProfileImage}",
                    user.Id, user.Name, user.Email, user.RegisteredAt, user.ProfileImage);

                return user.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with ID {UserId}", user.Id);
                return Guid.Empty;
            }
        }
    }
}
