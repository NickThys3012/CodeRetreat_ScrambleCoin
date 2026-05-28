namespace ScrambleCoin.Domain.Exceptions;

/// <summary>
/// Thrown when a requested tournament cannot be found.
/// </summary>
public sealed class TournamentNotFoundException : DomainException
{
    public Guid TournamentId { get; }

    public TournamentNotFoundException(Guid tournamentId)
        : base($"Tournament {tournamentId} was not found.") =>
        TournamentId = tournamentId;
}
