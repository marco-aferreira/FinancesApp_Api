using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace FinancesApp_Module_User.Application.Services;

public class DynamoImageService(IAmazonDynamoDB dynamo, string tableName) : IDynamoImageService
{
    public async Task SaveImageMetadataAsync(Guid userId, string imageId, string s3Key, long sizeBytes, CancellationToken token = default)
    {
        await dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "UserId",    new AttributeValue { S = userId.ToString() } },
                { "ImageId",   new AttributeValue { S = imageId } },
                { "S3Key",     new AttributeValue { S = s3Key } },
                { "SizeBytes", new AttributeValue { N = sizeBytes.ToString() } },
                { "CreatedAt", new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") } }
            }
        }, token);
    }
}
