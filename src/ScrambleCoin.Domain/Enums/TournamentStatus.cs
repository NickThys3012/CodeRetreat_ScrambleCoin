namespace ScrambleCoin.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a <see cref="ScrambleCoin.Domain.Tournaments.Tournament"/>.
/// </summary>
public enum TournamentStatus
{
    /// <summary>Tournament has been created but not yet started. Accepting participants.</summary>
    Pending,

    /// <summary>Tournament is in the round-robin group stage. Group games are being played.</summary>
    GroupStage,

    /// <summary>Group stage is complete. Knockout bracket games are being played.</summary>
    KnockoutStage,

    /// <summary>All knockout matches are complete; the winner has been determined.</summary>
    Completed,

    /// <summary>Tournament was cancelled by the organiser before completion.</summary>
    Cancelled
}
