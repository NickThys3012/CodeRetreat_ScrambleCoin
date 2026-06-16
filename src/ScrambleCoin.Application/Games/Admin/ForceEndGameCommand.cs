using MediatR;

namespace ScrambleCoin.Application.Games.Admin;

/// <summary>
/// Command to force-end a stuck or timed-out game.
/// <para>
/// If the game is <c>InProgress</c>, <see cref="Domain.Entities.Game.End"/> is called
/// and the winner is determined by the current coin scores — the timed-out bot will typically
/// have fewer coins because it missed its moves.
/// </para>
/// <para>
/// If the game is still <c>WaitingForBots</c>, the game is force-cancelled instead
/// (no ranking points are awarded).
/// </para>
/// <para>
/// Already-finished or cancelled games are silently ignored.
/// </para>
/// </summary>
/// <param name="GameId">The identifier of the game to force-end.</param>
public sealed record ForceEndGameCommand(Guid GameId) : IRequest;
