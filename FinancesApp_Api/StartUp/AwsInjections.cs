using Amazon;
using Amazon.DynamoDBv2;
using Amazon.S3;

namespace FinancesApp_Api.StartUp;

public static class AwsInjections
{
    public static IServiceCollection AddAwsServices(this IServiceCollection services, IConfiguration configuration)
    {
        var region = RegionEndpoint.GetBySystemName(configuration["Aws:Region"] ?? "us-east-1");
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(region));
        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(region));
        return services;
    }
}
