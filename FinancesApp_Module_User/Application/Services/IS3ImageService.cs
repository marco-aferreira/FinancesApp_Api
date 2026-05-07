namespace FinancesApp_Module_User.Application.Services;

public interface IS3ImageService
{
    Task<string> UploadAsync(Guid userId, byte[] data, string contentType, CancellationToken token = default);
    Task<string> GeneratePresignedUrlAsync(string s3Key, CancellationToken token = default);
}
