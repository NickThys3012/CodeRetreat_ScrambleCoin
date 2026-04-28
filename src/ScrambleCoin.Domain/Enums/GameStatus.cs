namespace ScrambleCoin.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a <see cref="Entities.Game"/>.
/// </summary>
public enum GameStatus
{
    /// <summary>The game lobby is open; waiting for both bots to register and submit lineups.</summary>
    WaitingForBots,

    /// <summary>Both players have submitted their lineups and the game is actively being played.</summary>
    InProgress,

    /// <summary>All 5 turns have been completed and a winner (or draw) has been determined.</summary>
    Finished
}
