using MediatR;

namespace ScrambleCoin.Application.Games.SpawnCoins;

/// <summary>
/// Triggers coin spawning for the current turn of the specified game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
public sealed record SpawnCoinsCommand(Guid GameId) : IRequest;
