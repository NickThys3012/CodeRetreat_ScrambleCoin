using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Tracks a bot's cumulative ranking points, win/draw/loss record,
/// and figurine unlock milestones earned through gameplay.
/// </summary>
public sealed class RankingTrack
{
    /// <summary>Maximum achievable ranking points. Further gains are silently ignored.</summary>
    public const int MaxPoints = 550;

    /// <summary>
    /// Figurine unlock milestone thresholds (in ranking points).
    /// A milestone is "hit" the first time a bot's points reach or cross the threshold.
    /// </summary>
    public static readonly IReadOnlyList<int> Milestones =
    [
        3, 9, 15, 24, 33, 45, 57, 69, 84, 99, 114, 129, 147, 165, 183,
        201, 219, 237, 258, 279, 300, 350, 400, 450, 500, 550
    ];

    private readonly List<int> _milestonesHit = [];

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Unique identifier of the bot. Used as the primary key.</summary>
    public Guid BotId { get; }

    /// <summary>Display name for this bot.</summary>
    public string BotName { get; private set; }

    // ── Ranking data ──────────────────────────────────────────────────────────

    /// <summary>Total accumulated ranking points (capped at <see cref="MaxPoints"/>).</summary>
    public int Points { get; private set; }

    /// <summary>Total number of games won.</summary>
    public int Wins { get; private set; }

    /// <summary>Total number of games drawn.</summary>
    public int Draws { get; private set; }

    /// <summary>Total number of games lost.</summary>
    public int Losses { get; private set; }

    /// <summary>Total number of games played.</summary>
    public int GamesPlayed { get; private set; }

    /// <summary>Ranking point milestones that have been hit (point values).</summary>
    public IReadOnlyList<int> MilestonesHit => _milestonesHit.AsReadOnly();

    // ── Constructors ──────────────────────────────────────────────────────────

    /// <summary>Creates a new ranking track for a bot with zero points.</summary>
    public RankingTrack(Guid botId, string botName)
    {
        if (botId == Guid.Empty)
            throw new DomainException("BotId must not be empty.");
        if (string.IsNullOrWhiteSpace(botName))
            throw new DomainException("BotName must not be null or whitespace.");

        BotId     = botId;
        BotName   = botName;
        Points    = 0;
        Wins      = 0;
        Draws     = 0;
        Losses    = 0;
        GamesPlayed = 0;
    }

    /// <summary>
    /// Reconstitution constructor used by the repository to rebuild a track from persisted data.
    /// </summary>
    public RankingTrack(
        Guid botId,
        string botName,
        int points,
        int wins,
        int draws,
        int losses,
        int gamesPlayed,
        IEnumerable<int> milestonesHit)
    {
        BotId       = botId;
        BotName     = botName;
        Points      = points;
        Wins        = wins;
        Draws       = draws;
        Losses      = losses;
        GamesPlayed = gamesPlayed;
        _milestonesHit.AddRange(milestonesHit);
    }

    // ── Recording results ─────────────────────────────────────────────────────

    /// <summary>Records a win: awards 3 ranking points and increments <see cref="Wins"/>.</summary>
    public void RecordWin()
    {
        AddPoints((int)GameResult.Win);
        Wins++;
        GamesPlayed++;
    }

    /// <summary>Records a draw: awards 2 ranking points and increments <see cref="Draws"/>.</summary>
    public void RecordDraw()
    {
        AddPoints((int)GameResult.Draw);
        Draws++;
        GamesPlayed++;
    }

    /// <summary>Records a loss: awards 1 ranking point and increments <see cref="Losses"/>.</summary>
    public void RecordLoss()
    {
        AddPoints((int)GameResult.Loss);
        Losses++;
        GamesPlayed++;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddPoints(int amount)
    {
        if (Points >= MaxPoints)
            return; // already capped — no further gains

        var pointsBefore = Points;
        Points = Math.Min(Points + amount, MaxPoints);

        // Detect newly crossed milestones
        foreach (var milestone in Milestones)
        {
            if (pointsBefore < milestone && Points >= milestone && !_milestonesHit.Contains(milestone))
                _milestonesHit.Add(milestone);
        }
    }
}
