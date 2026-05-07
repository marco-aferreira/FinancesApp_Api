using FinancesApp_CQRS.Interfaces;

namespace FinancesApp_Module_User.Application.Commands;

public record UpdateUserProfileImage(Guid UserId, string S3Key) : ICommand<bool>;
