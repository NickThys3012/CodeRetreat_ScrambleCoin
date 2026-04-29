using MediatR;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.PlacePiece;

/// <summary>Places an off-board piece at a valid entry-point tile during PlacePhase.</summary>
public sealed record PlacePieceCommand(
    Guid GameId,
    Guid PlayerId,
    Guid PieceId,
    Position TargetPosition) : IRequest;
