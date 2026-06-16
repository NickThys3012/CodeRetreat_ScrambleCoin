using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Defines the contract for CPU villain AI strategies.
/// A villain strategy decides what action a villain should take during their turn,
/// reading the (read-only) domain state and computing a single legal action.
/// </summary>
public interface IVillainStrategy
{
    /// <summary>
    /// Decides the villain's next action given the current game state.
    /// </summary>
    /// <param name="game">The current game state (read-only view).</param>
    /// <param name="villainPlayerId">The player ID controlled by the villain (PlayerTwo in solo games).</param>
    /// <returns>A <see cref="VillainAction"/> representing the villain's decision.</returns>
    VillainAction DecideAction(Game game, Guid villainPlayerId);
}

/// <summary>
/// Base type for all villain actions. Concrete record types form a closed discriminated union.
/// </summary>
public abstract record VillainAction;

/// <summary>
/// Villain decides to place a piece on the board at the specified position during PlacePhase.
/// </summary>
public sealed record PlacementAction(Guid PieceId, Position Position) : VillainAction;

/// <summary>
/// Villain decides to skip placement (e.g. at the 3-piece cap or no legal placement exists).
/// </summary>
public sealed record SkipPlacementAction : VillainAction;

/// <summary>
/// Villain decides to move one piece, expressed as one or more movement segments.
/// Each segment is an ordered list of step positions (not including the start position).
/// </summary>
public sealed record MovementAction(Guid PieceId, IReadOnlyList<IReadOnlyList<Position>> Segments) : VillainAction;

/// <summary>
/// Villain decides to skip movement (e.g. no on-board pieces, or no legal move exists).
/// </summary>
public sealed record SkipMovementAction : VillainAction;
