using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Defines the contract for CPU villain AI strategies.
/// A villain strategy decides what action a villain should take during their turn.
/// </summary>
public interface IVillainStrategy
{
    /// <summary>
    /// Decides the villain's next action given the current game state.
    /// </summary>
    /// <param name="game">The current game state (readonly view).</param>
    /// <param name="villainPlayerId">The player ID of the villain.</param>
    /// <returns>A <see cref="VillainAction"/> representing the villain's decision.</returns>
    VillainAction DecideAction(Game game, Guid villainPlayerId);
}

/// <summary>
/// Base type for all villain actions. Use discriminated unions via concrete record types.
/// </summary>
public abstract record VillainAction;

/// <summary>
/// Villain decides to place a piece on the board at the specified position.
/// </summary>
public sealed record PlacementAction(Guid PieceId, Position Position) : VillainAction;

/// <summary>
/// Villain decides to skip placement (e.g., already has 3 pieces on board or no valid placement).
/// </summary>
public sealed record SkipPlacementAction : VillainAction;

/// <summary>
/// Villain decides to move one piece with one or more movement segments.
/// </summary>
public sealed record MovementAction(Guid PieceId, IReadOnlyList<IReadOnlyList<Position>> Segments) : VillainAction;

/// <summary>
/// Villain decides to skip movement (e.g., no valid moves available).
/// </summary>
public sealed record SkipMovementAction : VillainAction;
