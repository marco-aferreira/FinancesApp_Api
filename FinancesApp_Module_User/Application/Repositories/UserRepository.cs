using FinanceAppDatabase.DbConnection;
using FinancesApp_Module_User.Domain;
using Microsoft.Data.SqlClient;

namespace FinancesApp_Module_User.Application.Repositories;
public class UserRepository : IUserRepository
{
    private readonly ICommandFactory _commandFactory;
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public UserRepository(IDbConnectionFactory dbConnectionFactory,
                          ICommandFactory commandFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _commandFactory = commandFactory;
    }
    public async Task<Guid> CreateUserAsync(User user,
                                            SqlConnection? connection = null,
                                            CancellationToken token = default)
    {
        const string InsertCommandText = @"INSERT INTO [FinanceApp].[dbo].[Users] 
                                            (Id, Name, Email, RegisteredAt, ModifiedAt, DateOfBirth)
                                            OUTPUT INSERTED.Id
                                            VALUES 
                                            (@Id, @Name, @Email, @RegisteredAt, @ModifiedAt, @DateOfBirth)";
        var parameters = new Dictionary<string, object>
        {
            { "@Id", user.Id },
            { "@Name", user.Name },
            { "@Email", user.Email },
            { "@RegisteredAt", user.RegisteredAt },
            { "@ModifiedAt", user.ModifiedAt },
            { "@DateOfBirth", user.DateOfBirth }
        };

        var userId = await _commandFactory.ExecuteAsync(
            commandText: InsertCommandText,
            options: new CreateSqlCommandOptions
            {
                Parameters = [.. parameters.Select(p => new SqlParameter(p.Key, p.Value))]
            },
            operation: async command =>
            {
                var res = await command.ExecuteScalarAsync();
                Guid insertedId = res is null ? Guid.Empty : (Guid)res;

                return insertedId;
            },
            token);

        return userId;
    }

    public async Task<bool> UpdateUserAsync(User user,
                                            SqlConnection? connection = null,
                                            CancellationToken token = default)
    {
        const string UpdateCommandText = @"UPDATE [FinanceApp].[dbo].[Users]
                                           SET Name = COALESCE(NULLIF(@Name, ''), Name),
                                               Email = COALESCE(NULLIF(@Email, ''), Email),
                                               ModifiedAt = @ModifiedAt
                                           WHERE Id = @Id";

        var parameters = new Dictionary<string, object>
        {
            { "@Id", user.Id },
            { "@Name", user.Name },
            { "@Email", user.Email },
            { "@ModifiedAt", user.ModifiedAt },
        };

        var rowsAffected = await _commandFactory.ExecuteAsync(
            commandText: UpdateCommandText,
            options: new CreateSqlCommandOptions
            {
                Parameters = [.. parameters.Select(p => new SqlParameter(p.Key, p.Value))]
            },
            operation: async command => await command.ExecuteNonQueryAsync(token),
            token);

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateProfileImageAsync(Guid userId,
                                                    string s3Key,
                                                    SqlConnection? connection = null,
                                                    CancellationToken token = default)
    {
        const string UpdateCommandText = @"UPDATE [FinanceApp].[dbo].[Users]
                                           SET ProfileImage = @ProfileImage
                                           WHERE Id = @Id";
        try
        {
            var rowsAffected = await _commandFactory.ExecuteAsync(
            commandText: UpdateCommandText,
            options: new CreateSqlCommandOptions
            {
                Parameters =
                [
                    new("@Id", System.Data.SqlDbType.UniqueIdentifier) { Value = userId },
                    new("@ProfileImage", System.Data.SqlDbType.NVarChar) { Value =  s3Key }
                ]
            },
            operation: async command => await command.ExecuteNonQueryAsync(token),
            token);
            return rowsAffected > 0;

        }
        catch (Exception ex)
        {
            throw;
        }

    }

    public async Task<bool> DeleteUserAsync(Guid userId,
                                            SqlConnection? connection = null,
                                            CancellationToken token = default)
    {
        const string DeleteCommandText = @"DELETE FROM [FinanceApp].[dbo].[Users] 
                                           WHERE Id = @Id";

        var rowsAffected = await _commandFactory.ExecuteAsync(
            commandText: DeleteCommandText,
            options: new CreateSqlCommandOptions
            {
                Parameters = [new("@Id", System.Data.SqlDbType.UniqueIdentifier) { Value = userId }]
            },
            operation: async command => await command.ExecuteNonQueryAsync(token),
            token);

        return rowsAffected > 0;
    }
}
