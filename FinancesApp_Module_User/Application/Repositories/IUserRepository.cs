using FinancesApp_Module_User.Domain;
using Microsoft.Data.SqlClient;

namespace FinancesApp_Module_User.Application.Repositories;
public interface IUserRepository
{
    Task<Guid> CreateUserAsync(User user, SqlConnection? connection = null, CancellationToken token = default);
    Task<bool> DeleteUserAsync(Guid userId, SqlConnection? connection = null, CancellationToken token = default);
    Task<bool> UpdateUserAsync(User user, SqlConnection? connection = null, CancellationToken token = default);
    Task<bool> UpdateProfileImageAsync(Guid userId, string s3Key, SqlConnection? connection = null, CancellationToken token = default);
}