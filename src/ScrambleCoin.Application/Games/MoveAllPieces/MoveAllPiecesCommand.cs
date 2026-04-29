using MediatR;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.MoveAllPieces;

/// <summary>
/// Represents the move actions for a single piece: which piece, and the list of movement segments.
/// Each segment is a list of positions (steps) the piece takes during one move action.
/// </summary>
/// <param name="PieceId">The unique identifier of the piece to move.</param>
/// <param name="Segments">
/// The list of move segments, one per <c>MovesPerTurn</c>.
/// Each segment contains the positions visited, in order, during that move action.
/// </param>
public sealed record PieceMovement(
    Guid PieceId,
    IReadOnlyList<IReadOnlyList<Position>> Segments);

/// <summary>
/// Submits all move actions for the specified player's on-board pieces during the MovePhase.
/// Every on-board piece must be present in <see cref="Moves"/>.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The player submitting their moves.</param>
/// <param name="Moves">One <see cref="PieceMovement"/> entry per on-board piece.</param>
public sealed record MoveAllPiecesCommand(
    Guid GameId,
    Guid PlayerId,
    IReadOnlyList<PieceMovement> Moves) : IRequest;
