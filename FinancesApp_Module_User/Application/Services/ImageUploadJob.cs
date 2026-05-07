namespace FinancesApp_Module_User.Application.Services;

public record ImageUploadJob(Guid UserId, byte[] ImageData, string ContentType);
