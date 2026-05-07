namespace FinancesApp_Module_User.Application.Services;

public interface IImageValidator
{
    (bool IsValid, string? Error) Validate(byte[] data, string contentType);
}
