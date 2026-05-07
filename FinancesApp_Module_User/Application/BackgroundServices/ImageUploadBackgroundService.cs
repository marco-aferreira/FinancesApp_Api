using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_User.Application.Commands;
using FinancesApp_Module_User.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace FinancesApp_Module_User.Application.BackgroundServices;

public class ImageUploadBackgroundService(ChannelReader<ImageUploadJob> reader,
                                          IImageValidator validator,
                                          IS3ImageService s3,
                                          IDynamoImageService dynamo,
                                          IServiceScopeFactory scopeFactory,
                                          ILogger<ImageUploadBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in reader.ReadAllAsync(stoppingToken))
            await ProcessJobAsync(job, stoppingToken);
    }

    private async Task ProcessJobAsync(ImageUploadJob job, CancellationToken token)
    {
        var (isValid, error) = validator.Validate(job.ImageData, job.ContentType);
        if (!isValid)
        {
            logger.LogWarning("Image validation failed for user {UserId}: {Error}", job.UserId, error);
            return;
        }

        var imageId = Guid.NewGuid().ToString();
        string s3Key;

        try
        {
            s3Key = await s3.UploadAsync(job.UserId, job.ImageData, job.ContentType, token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "S3 upload failed for user {UserId}", job.UserId);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<UpdateUserProfileImage, bool>>();
            await handler.Handle(new UpdateUserProfileImage(job.UserId, s3Key), token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile image command failed for user {UserId}", job.UserId);
            return;
        }

        try
        {
            await dynamo.SaveImageMetadataAsync(job.UserId, imageId, s3Key, job.ImageData.Length, token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DynamoDB write failed for user {UserId} image {ImageId}", job.UserId, imageId);
        }

        logger.LogInformation("Profile image saved for user {UserId}: {S3Key}", job.UserId, s3Key);
    }
}
