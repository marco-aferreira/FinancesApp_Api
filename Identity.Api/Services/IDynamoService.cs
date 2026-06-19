using Identity.Api.Contracts;

namespace Identity.Api.Services;

public interface IDynamoService
{
    Task SaveUserSessionReference(SessionTokenReference tokenReference, CancellationToken token = default);
    Task<SessionLookup> GetByRefHash(string refHash, CancellationToken token = default);

    /// <summary>
    /// Marks the session row as revoked. Returns the owning UserId so the caller can evict the
    /// user's active-ref cache, or null when no row exists for the hash (already gone / unknown).
    /// </summary>
    Task<Guid?> RevokeByRefHash(string refHash, CancellationToken token = default);
}
