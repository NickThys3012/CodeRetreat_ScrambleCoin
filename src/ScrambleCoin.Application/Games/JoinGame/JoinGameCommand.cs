using MediatR;

namespace ScrambleCoin.Application.Games.JoinGame;

/// <summary>
/// Bot command to join an existing game and submit a lineup.
/// </summary>
/// <param name="GameId">The game to join.</param>
/// <param name="LineupPieceNames">Ordered list of exactly 5 piece names (e.g. "Mickey", "Minnie", ...).</param>
public sealed record JoinGameCommand(
    Guid GameId,
    IReadOnlyList<string> LineupPieceNames) : IRequest<JoinGameResult>;
