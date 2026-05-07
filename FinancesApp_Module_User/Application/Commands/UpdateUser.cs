using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_User.Domain;

namespace FinancesApp_Module_User.Application.Commands;

public record UpdateUser(User User, byte[]? ImageData = null, string? ContentType = null) : ICommand<bool>;