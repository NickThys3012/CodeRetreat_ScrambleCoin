using MediatR;
namespace ScrambleCoin.Application.Games.SoloMode.CreateSoloGame;

/// <summary>
/// Command to create a solo game where a bot challenges a specific villain.
/// Validates that the villain is available (not locked) for the bot.
/// </summary>
public sealed record CreateSoloGameCommand(
    Guid BotId,
    string VillainId) : IRequest<CreateSoloGameResult>;

/// <summary>Result of <see cref="CreateSoloGameCommand"/>.</summary>
public sealed record CreateSoloGameResult(
    Guid GameId,
    string VillainId,
    string GameMode = "Solo");
