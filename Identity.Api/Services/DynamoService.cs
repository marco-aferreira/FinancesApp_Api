using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Identity.Api.Contracts;
using System.Text.Json;

namespace Identity.Api.Services;

public class DynamoService(IAmazonDynamoDB dynamo, string tableName) : IDynamoService
{
    public async Task SaveUserSessionReference(SessionTokenReference session, CancellationToken token = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["refHash"] = new() { S = session.RefHash },
            ["userId"] = new() { S = session.UserId.ToString() },
            ["login"] = new() { S = session.Login },
            ["customClaims"] = new() { S = JsonSerializer.Serialize(session.CustomClaims) },
            ["createdAt"] = new() { N = session.CreatedAt.ToString() },
            ["expiresAt"] = new() { N = session.ExpiresAt.ToString() },
            ["revoked"] = new() { BOOL = session.Revoked }
        };

        if (session.AccountIds.Count > 0)
        {
            item["accountIds"] = new AttributeValue
            {
                SS = [.. session.AccountIds.Select(id => id.ToString())]
            };
        }

        await dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = item
        }, token);
    }
    public async Task<Guid?> RevokeByRefHash(string refHash, CancellationToken token = default)
    {
        try
        {
            var response = await dynamo.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["refHash"] = new() { S = refHash }
                },
                UpdateExpression = "SET revoked = :revoked",
                ConditionExpression = "attribute_exists(refHash)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":revoked"] = new() { BOOL = true }
                },
                ReturnValues = ReturnValue.ALL_NEW
            }, token);

            if (response.Attributes.TryGetValue("userId", out var userIdAttr)
                && Guid.TryParse(userIdAttr.S, out var userId))
                return userId;

            return null;
        }
        catch (ConditionalCheckFailedException)
        {
            // No row for this hash — nothing to revoke. Idempotent: treat as a no-op.
            return null;
        }
    }

    public async Task<SessionLookup> GetByRefHash(string refHash,
                                                  CancellationToken token = default)
    {
        var result = new SessionLookup();

        Dictionary<string, AttributeValue> key = new()
        {
            ["refHash"] = new() { S = refHash },
        };

        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = tableName,
            Key = key,
            ConsistentRead = true
        }, token);

        var item = response?.Item;

        if (item is null || item.Count == 0)
            return result;

        foreach (var pair in item)
        {
            var value = pair.Value;
            switch (pair.Key)
            {
                case "refHash":
                    break;

                case "userId":
                    if (Guid.TryParse(value.S, out var userId))
                        result.UserId = userId;
                    break;

                case "login":
                    result.Login = value.S ?? "";
                    break;

                case "accountIds":
                    if (value.SS is { Count: > 0 })
                        result.AccountIds = value.SS
                            .Where(s => Guid.TryParse(s, out _))
                            .Select(Guid.Parse)
                            .ToList();
                    break;

                case "customClaims":
                    if (!string.IsNullOrEmpty(value.S))
                        result.CustomClaims =
                            JsonSerializer.Deserialize<Dictionary<string, object>>(value.S) ?? [];
                    break;

                case "createdAt":
                    if (long.TryParse(value.N, out var createdUnix))
                        result.CreatedAt = DateTimeOffset.FromUnixTimeSeconds(createdUnix);
                    break;

                case "expiresAt":
                    if (long.TryParse(value.N, out var expiresUnix))
                        result.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
                    break;

                case "revoked":
                    result.Revoked = value.BOOL;
                    break;
            }
        }

        return result;
    }
}