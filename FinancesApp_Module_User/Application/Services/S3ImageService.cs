using Amazon.S3;
using Amazon.S3.Model;

namespace FinancesApp_Module_User.Application.Services;

public class S3ImageService(IAmazonS3 s3, string bucketName) : IS3ImageService
{
    public async Task<string> UploadAsync(Guid userId, byte[] data, string contentType, CancellationToken token = default)
    {
        var key = $"profile-images/{userId}/profile";

        using var stream = new MemoryStream(data);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        }, token);

        return key;
    }

    public Task<string> GeneratePresignedUrlAsync(string s3Key, CancellationToken token = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = s3Key,
            Expires = DateTime.UtcNow.AddMinutes(1),
            Verb = HttpVerb.GET
        };
        return Task.FromResult(s3.GetPreSignedURL(request));
    }
}
