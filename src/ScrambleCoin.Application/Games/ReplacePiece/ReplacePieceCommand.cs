using MediatR;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.ReplacePiece;

/// <summary>
/// Removes an on-board piece and places a different lineup piece at the given position during PlacePhase.
/// </summary>
public sealed record ReplacePieceCommand(
    Guid GameId,
    Guid PlayerId,
    Guid ExistingPieceId,
    Guid NewPieceId,
    Position TargetPosition) : IRequest;
