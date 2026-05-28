namespace ScrambleCoin.Application.Ranking.GetLeaderboard;

/// <summary>
/// A single row in the ranking leaderboard response.
/// </summary>
/// <param name="BotId">Unique identifier of the bot.</param>
/// <param name="BotName">Display name of the bot.</param>
/// <param name="Points">Total accumulated ranking points.</param>
/// <param name="Wins">Total wins.</param>
/// <param name="Draws">Total draws.</param>
/// <param name="Losses">Total losses.</param>
/// <param name="GamesPlayed">Total games played.</param>
/// <param name="Rank">1-indexed position on the leaderboard (1 = highest points).</param>
public sealed record LeaderboardEntryDto(
    Guid   BotId,
    string BotName,
    int    Points,
    int    Wins,
    int    Draws,
    int    Losses,
    int    GamesPlayed,
    int    Rank);
