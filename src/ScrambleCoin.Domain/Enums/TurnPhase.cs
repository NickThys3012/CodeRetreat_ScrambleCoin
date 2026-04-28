namespace ScrambleCoin.Domain.Enums;

/// <summary>
/// Represents the three sequential phases that make up a single turn in a Scramblecoin game.
/// Each turn must progress through all three phases in order:
/// <see cref="CoinSpawn"/> → <see cref="PlacePhase"/> → <see cref="MovePhase"/>.
/// </summary>
public enum TurnPhase
{
    /// <summary>
    /// The first phase of every turn. Coins are spawned onto the board during this phase.
    /// </summary>
    CoinSpawn,

    /// <summary>
    /// The second phase of every turn. Players may place or replace pieces on the board.
    /// This phase is skippable by individual players, but the phase itself is mandatory.
    /// </summary>
    PlacePhase,

    /// <summary>
    /// The third and final phase of every turn. Players move their pieces on the board.
    /// After this phase completes, the turn number increments (or the game ends after turn 5).
    /// </summary>
    MovePhase
}
