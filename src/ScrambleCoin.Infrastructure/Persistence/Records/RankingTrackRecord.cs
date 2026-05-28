namespace ScrambleCoin.Infrastructure.Persistence.Records;

/// <summary>
/// EF Core persistence POCO for the <see cref="ScrambleCoin.Domain.Entities.RankingTrack"/> entity.
/// </summary>
public sealed class RankingTrackRecord
{
    /// <summary>Bot identifier — primary key.</summary>
    public Guid BotId { get; set; }

    /// <summary>Display name of the bot.</summary>
    public string BotName { get; set; } = string.Empty;

    /// <summary>Total accumulated ranking points.</summary>
    public int Points { get; set; }

    /// <summary>Total wins.</summary>
    public int Wins { get; set; }

    /// <summary>Total draws.</summary>
    public int Draws { get; set; }

    /// <summary>Total losses.</summary>
    public int Losses { get; set; }

    /// <summary>Total games played.</summary>
    public int GamesPlayed { get; set; }

    /// <summary>
    /// JSON-serialised list of milestone point thresholds already hit by this bot.
    /// Stored as a comma-separated integers string, e.g. "3,9,15".
    /// </summary>
    public string MilestonesHitJson { get; set; } = "[]";
}
