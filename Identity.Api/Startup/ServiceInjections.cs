using Amazon.DynamoDBv2;
using Identity.Api.Filters;
using Identity.Api.Handlers;
using Identity.Api.Services;

namespace Identity.Api.Startup;

public static class ServiceInjections
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        var tableName = configuration["Aws:DynamoDb:TableName"]
            ?? throw new InvalidOperationException("Aws:DynamoDb:TableName not found in configuration.");

        services.AddSingleton<JwtClaimsDecryptor>();
        services.AddSingleton<TokenHandler>();
        services.AddSingleton<IDynamoService>(sp =>
            new DynamoService(sp.GetRequiredService<IAmazonDynamoDB>(), tableName));

        services.AddScoped<InternalSecretFilter>();

        return services;
    }
}
