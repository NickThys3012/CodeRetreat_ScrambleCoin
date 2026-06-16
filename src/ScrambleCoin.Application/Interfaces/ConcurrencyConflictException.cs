namespace ScrambleCoin.Application.Interfaces;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.SaveChangesAsync"/> when a write conflicts with a
/// concurrent update (optimistic concurrency violation). Callers that need idempotency
/// should reload the aggregate and return the freshly persisted state.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
