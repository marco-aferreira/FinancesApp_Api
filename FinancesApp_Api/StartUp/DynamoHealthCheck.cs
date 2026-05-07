using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinancesApp_Api.StartUp;

public class DynamoHealthCheck(IAmazonDynamoDB dynamo, IConfiguration config) : IHealthCheck
{
    private readonly string _tableName = config["Aws:DynamoDb:TableName"]
        ?? throw new InvalidOperationException("Aws:DynamoDb:TableName not configured");

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        try
        {
            var response = await dynamo.DescribeTableAsync(_tableName, token);
            var status = response.Table.TableStatus;

            return status == TableStatus.ACTIVE
                ? HealthCheckResult.Healthy($"DynamoDB table '{_tableName}' is ACTIVE.")
                : HealthCheckResult.Degraded($"DynamoDB table '{_tableName}' status: {status}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"DynamoDB table '{_tableName}' unreachable.", ex);
        }
    }
}
