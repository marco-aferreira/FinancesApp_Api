using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinancesApp_Api.StartUp;

public class S3HealthCheck(IAmazonS3 s3, IConfiguration config) : IHealthCheck
{
    private readonly string _bucketName = config["Aws:S3:BucketName"]
        ?? throw new InvalidOperationException("Aws:S3:BucketName not configured");

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        try
        {
            await s3.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = _bucketName }, token);
            return HealthCheckResult.Healthy($"S3 bucket '{_bucketName}' reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"S3 bucket '{_bucketName}' unreachable.", ex);
        }
    }
}
