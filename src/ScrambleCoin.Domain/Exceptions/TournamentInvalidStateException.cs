namespace ScrambleCoin.Domain.Exceptions;

/// <summary>
/// Thrown when a tournament operation is attempted in an incompatible lifecycle state.
/// </summary>
public sealed class TournamentInvalidStateException : DomainException
{
    public TournamentInvalidStateException(string message)
        : base(message) { }
}
