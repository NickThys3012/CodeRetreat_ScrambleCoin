using MediatR;

namespace ScrambleCoin.Application.Games.SubmitPlacement;

/// <summary>
/// Submits a placement action (place, replace, or skip) during PlacePhase.
/// Implements <see cref="IGameStateChangingCommand"/> so that the SignalR broadcast
/// pipeline behaviour fires after every successful placement action.
/// </summary>
public sealed record SubmitPlacementCommand(
    Guid GameId,
    Guid BotToken,
    string? Action,
    Guid? PieceId,
    Guid? ReplacedPieceId,
    PositionRequest? Position) : IRequest<PlacementResult>, IGameStateChangingCommand;

public sealed record PositionRequest(int Row, int Col);
