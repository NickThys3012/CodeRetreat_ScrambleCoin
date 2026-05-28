namespace ScrambleCoin.Domain.Tournaments;

/// <summary>
/// Represents a bot registered as a participant in a tournament.
/// Stores the bot's identifier and their piece lineup for tournament games.
/// </summary>
public sealed class TournamentParticipant
{
    /// <summary>
    /// The bot's stable identity across the tournament.
    /// Used as the <c>X-Bot-Token</c> base for game registrations.
    /// </summary>
    public Guid BotId { get; }

    /// <summary>Human-readable name for this bot (for display purposes).</summary>
    public string BotName { get; }

    /// <summary>The piece lineup the bot will use for all their tournament games.</summary>
    public IReadOnlyList<string> Lineup { get; }

    public TournamentParticipant(Guid botId, string botName, IReadOnlyList<string> lineup)
    {
        BotId = botId;
        BotName = botName;
        Lineup = lineup;
    }
}
