namespace ScrambleCoin.Application.Interfaces;

/// <summary>
/// Abstraction over a shared persistence transaction boundary.
/// Implementations (e.g. <c>ScrambleCoinDbContext</c>) commit all staged changes
/// to the underlying store in a single round-trip.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all staged changes atomically.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
