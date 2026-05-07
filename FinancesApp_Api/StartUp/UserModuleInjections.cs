using Amazon.DynamoDBv2;
using Amazon.S3;
using FinancesApp_CQRS.Interfaces;
using FinancesApp_CQRS.Projections;
using FinancesApp_Module_User.Application.BackgroundServices;
using FinancesApp_Module_User.Application.Commands;
using FinancesApp_Module_User.Application.Commands.Handlers;
using FinancesApp_Module_User.Application.Projections;
using FinancesApp_Module_User.Application.Queries;
using FinancesApp_Module_User.Application.Queries.Handlers;
using FinancesApp_Module_User.Application.Repositories;
using FinancesApp_Module_User.Application.Services;
using FinancesApp_Module_User.Domain;
using FinancesApp_Module_User.Domain.Events;
using System.Threading.Channels;

namespace FinancesApp_Api.StartUp;

public static class UserModuleInjections
{
    public static IServiceCollection AddUserModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IUserReadRepository, UserReadRepository>();
        services.AddSingleton<UserProjection>();

        services.AddSingleton<IImageValidator, ImageValidator>();

        services.AddSingleton<IS3ImageService>(sp => new S3ImageService(
            sp.GetRequiredService<IAmazonS3>(),
            configuration["Aws:S3:BucketName"]
                ?? throw new InvalidOperationException("Aws:S3:BucketName is not configured.")));

        services.AddSingleton<IDynamoImageService>(sp => new DynamoImageService(
            sp.GetRequiredService<IAmazonDynamoDB>(),
            configuration["Aws:DynamoDb:TableName"]
                ?? throw new InvalidOperationException("Aws:DynamoDb:TableName is not configured.")));

        var channel = Channel.CreateUnbounded<ImageUploadJob>(new UnboundedChannelOptions { SingleReader = true });
        services.AddSingleton(channel);
        services.AddSingleton(channel.Writer);
        services.AddSingleton(channel.Reader);

        services.AddHostedService<ImageUploadBackgroundService>();

        services.AddScoped<ICommandHandler<CreateUser, Guid>, CreateUserHandler>();
        services.AddScoped<ICommandHandler<UpdateUser, bool>, UpdateUserHandler>();
        services.AddScoped<ICommandHandler<DeleteUser, bool>, DeleteUserHandler>();
        services.AddScoped<ICommandHandler<UpdateUserProfileImage, bool>, UpdateUserProfileImageHandler>();

        services.AddScoped<IQueryHandler<GetUserById, User>, GetUserByIdHandler>();
        services.AddScoped<IQueryHandler<GetUsers, IReadOnlyList<User>>, GetUsersHandler>();
        services.AddScoped<IQueryHandler<GetUserByEmail, User>, GetUserByEmailHandler>();

        return services;
    }

    public static WebApplication AddUserProjections(this WebApplication app)
    {
        var dispatcher = app.Services.GetRequiredService<IEventDispatcher>();
        var projection = app.Services.GetRequiredService<UserProjection>();

        dispatcher.Register<UserCreatedEvent>(projection);
        dispatcher.Register<UserUpdatedEvent>(projection);
        dispatcher.Register<UserDeletedEvent>(projection);
        dispatcher.Register<UserProfileImageUpdatedEvent>(projection);

        return app;
    }
}
