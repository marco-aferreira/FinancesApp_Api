namespace Identity.Api.Startup;

public static class RedisInjections
{
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString not configured.");

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "IdentityApi:";
        });

        return services;
    }
}
