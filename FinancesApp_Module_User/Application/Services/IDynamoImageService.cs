namespace FinancesApp_Module_User.Application.Services;

public interface IDynamoImageService
{
    Task SaveImageMetadataAsync(Guid userId, string imageId, string s3Key, long sizeBytes, CancellationToken token = default);
}
