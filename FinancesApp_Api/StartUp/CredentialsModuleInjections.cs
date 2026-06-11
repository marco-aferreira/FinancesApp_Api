using FinancesApp_Api.Jwt;
using FinancesApp_CQRS.Interfaces;
using FinancesApp_CQRS.Projections;
using FinancesApp_Module_Credentials.Application.Commands;
using FinancesApp_Module_Credentials.Application.Commands.Handlers;
using FinancesApp_Module_Credentials.Application.Projections;
using FinancesApp_Module_Credentials.Application.Queries;
using FinancesApp_Module_Credentials.Application.Queries.Handlers;
using FinancesApp_Module_Credentials.Application.Repositories;
using FinancesApp_Module_Credentials.Domain;
using FinancesApp_Module_Credentials.Domain.Events;

namespace FinancesApp_Api.StartUp;

public static class CredentialsModuleInjections
{
    public static IServiceCollection AddCredentialsModule(this IServiceCollection services)
    {
        // Repositories
        services.AddSingleton<IUserCredentialsRepository, UserCredentialsRepository>();
        services.AddSingleton<IUserCredentialsReadRepository, UserCredentialsReadRepository>();
        services.AddSingleton<IUserCredentialsTotpReadRepository, UserCredentialsTotpReadRepository>();

        // Services
        services.AddSingleton<TotpService>();
        services.AddScoped<TotpValidator>();

        // Projections
        services.AddSingleton<CredentialsProjection>();
        services.AddSingleton<TotpProjection>();

        // Credentials commands
        services.AddScoped<ICommandHandler<RegisterUserCredentials, Guid>, RegisterUserCredentialsHandler>();
        services.AddScoped<ICommandHandler<UpdateUserCredentials, bool>, UpdateUserCredentialsHandler>();
        services.AddScoped<ICommandHandler<DeleteUserCredentials, bool>, DeleteUserCredentialsHandler>();

        // TOTP commands
        services.AddScoped<ICommandHandler<TotpCredentialCreated, bool>, TotpCredentialCreatedHandler>();
        services.AddScoped<ICommandHandler<InvalidateTotpCredential, bool>, InvalidateTotpCredentialHandler>();

        // Logout
        services.AddScoped<ICommandHandler<LogoutUser, bool>, LogoutUserHandler>();

        // Projection rebuild
        services.AddScoped<ICommandHandler<RebuildCredentialsProjection, bool>, RebuildCredentialsProjectionHandler>();

        // Credentials queries
        services.AddScoped<IQueryHandler<GetUserCredentialsByLogin, UserCredentials>, GetUserCredentialsByLoginHandler>();
        services.AddScoped<IQueryHandler<GetUserCredentialsByUserId, UserCredentials>, GetUserCredentialsByUserIdHandler>();

        // TOTP queries
        services.AddScoped<IQueryHandler<GetActiveUserTotp, UserCredentialsTotp?>, GetActiveUserTotpHandler>();

        return services;
    }

    public static WebApplication AddCredentialsProjections(this WebApplication app)
    {
        var dispatcher = app.Services.GetRequiredService<IEventDispatcher>();

        var credentialsProjection = app.Services.GetRequiredService<CredentialsProjection>();
        dispatcher.Register<CredentialsRegisteredEvent>(credentialsProjection);
        dispatcher.Register<CredentialsPasswordChangedEvent>(credentialsProjection);
        dispatcher.Register<CredentialsDeletedEvent>(credentialsProjection);

        var totpProjection = app.Services.GetRequiredService<TotpProjection>();
        dispatcher.Register<TotpCredentialCreatedEvent>(totpProjection);
        dispatcher.Register<TotpCredentialInvalidatedEvent>(totpProjection);

        return app;
    }
}
