using MediatR;

namespace ScrambleCoin.Application.Games.SubmitPlacement;

public sealed record SubmitPlacementCommand(
    Guid GameId,
    Guid PlayerId,
    string? Action,
    Guid? PieceId,
    Guid? ReplacedPieceId,
    PositionRequest? Position) : IRequest<PlacementResult>;

public sealed record PositionRequest(int Row, int Col);
