namespace ScrambleCoin.Application.Ranking.GetAllBotsProgress;

/// <summary>
/// Summary of a single bot's solo-mode (villain-path) progress.
/// </summary>
/// <param name="BotId">Unique bot identifier.</param>
/// <param name="BotName">Bot display name (from ranking track, or short ID if unavailable).</param>
/// <param name="VillainsDefeated">Total number of distinct villains this bot has defeated.</param>
/// <param name="PiecesUnlocked">Total number of pieces unlocked via villain defeats.</param>
public sealed record BotProgressDto(
    Guid BotId,
    string BotName,
    int VillainsDefeated,
    int PiecesUnlocked);
