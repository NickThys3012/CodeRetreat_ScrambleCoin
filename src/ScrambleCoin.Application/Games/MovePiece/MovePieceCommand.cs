using MediatR;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.MovePiece;

/// <summary>
/// Submits a single piece's move action(s) during MovePhase.
/// Implements <see cref="IGameStateChangingCommand"/> so that the SignalR broadcast
/// pipeline behaviour fires after every successful move.
/// </summary>
/// <param name="GameId">The game identifier.</param>
/// <param name="BotToken">The bearer token of the bot submitting the move.</param>
/// <param name="PieceId">The piece to move.</param>
/// <param name="Segments">
/// One segment per <c>MovesPerTurn</c>. Each segment is an ordered list of positions
/// the piece steps through during that move action (not including the starting position).
/// </param>
public sealed record MovePieceCommand(
    Guid GameId,
    Guid BotToken,
    Guid PieceId,
    IReadOnlyList<IReadOnlyList<Position>> Segments) : IRequest<MoveResult>, IGameStateChangingCommand;
