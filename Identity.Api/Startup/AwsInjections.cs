using Amazon;
using Amazon.DynamoDBv2;

namespace Identity.Api.Startup;

public static class AwsInjections
{
    public static IServiceCollection AddAwsServices(this IServiceCollection services, IConfiguration configuration)
    {
        var region = RegionEndpoint.GetBySystemName(configuration["Aws:Region"] ?? "us-east-1");
        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(region));
        return services;
    }
}
