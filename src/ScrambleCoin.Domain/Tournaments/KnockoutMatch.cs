namespace ScrambleCoin.Domain.Tournaments;

/// <summary>
/// Represents a match in the single-elimination knockout bracket.
/// </summary>
public sealed class KnockoutMatch
{
    /// <summary>Unique identifier for this match.</summary>
    public Guid Id { get; }

    /// <summary>Knockout round number (1 = first round, 2 = semi-final, etc.).</summary>
    public int Round { get; }

    /// <summary>
    /// Zero-based position of this match within its round.
    /// Used to determine which match in the next round receives the winner.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Bot ID of the first participant; <c>null</c> when TBD (waiting for a previous match result).
    /// <see cref="Guid.Empty"/> means this slot is a bye (the opposing bot advances automatically).
    /// </summary>
    public Guid? BotOne { get; private set; }

    /// <summary>
    /// Bot ID of the second participant; <c>null</c> when TBD.
    /// <see cref="Guid.Empty"/> means this slot is a bye.
    /// </summary>
    public Guid? BotTwo { get; private set; }

    /// <summary>Player-slot ID for BotOne inside the game. <c>null</c> until a game is assigned.</summary>
    public Guid? BotOnePlayerId { get; private set; }

    /// <summary>Player-slot ID for BotTwo inside the game. <c>null</c> until a game is assigned.</summary>
    public Guid? BotTwoPlayerId { get; private set; }

    /// <summary>Authentication token for BotOne. <c>null</c> until a game is assigned.</summary>
    public Guid? BotOneToken { get; private set; }

    /// <summary>Authentication token for BotTwo. <c>null</c> until a game is assigned.</summary>
    public Guid? BotTwoToken { get; private set; }

    /// <summary>The game created for this match; <c>null</c> until assigned.</summary>
    public Guid? GameId { get; private set; }

    /// <summary>Whether this match has been completed (or resolved via bye).</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Bot ID of the winner; <c>null</c> if not complete or drawn.</summary>
    public Guid? WinnerId { get; private set; }

    /// <summary>Whether the match was a draw (both players advance the higher-seeded bot).</summary>
    public bool IsDraw { get; private set; }

    /// <summary>
    /// True when one slot is a bye — the other bot automatically advances without playing.
    /// </summary>
    public bool IsBye => BotOne == Guid.Empty || BotTwo == Guid.Empty;

    public KnockoutMatch(Guid id, int round, int position, Guid? botOne, Guid? botTwo)
    {
        Id = id;
        Round = round;
        Position = position;
        BotOne = botOne;
        BotTwo = botTwo;
    }

    /// <summary>
    /// Sets the participants for a TBD slot (when a previous round produces a winner).
    /// </summary>
    public void SetParticipant(int slot, Guid botId)
    {
        if (slot == 1)
            BotOne = botId;
        else if (slot == 2)
            BotTwo = botId;
        else
            throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be 1 or 2.");
    }

    /// <summary>Assigns a real game to this knockout match.</summary>
    public void AssignGame(Guid gameId, Guid botOnePlayerId, Guid botOneToken, Guid botTwoPlayerId, Guid botTwoToken)
    {
        GameId = gameId;
        BotOnePlayerId = botOnePlayerId;
        BotOneToken = botOneToken;
        BotTwoPlayerId = botTwoPlayerId;
        BotTwoToken = botTwoToken;
    }

    /// <summary>Records the result of this knockout match.</summary>
    public void RecordResult(Guid? winnerId, bool isDraw)
    {
        IsCompleted = true;
        WinnerId = winnerId;
        IsDraw = isDraw;
    }

    /// <summary>Resolves a bye match — the present bot advances automatically.</summary>
    public void ResolveBye()
    {
        if (!IsBye)
            throw new InvalidOperationException($"Knockout match {Id} is not a bye.");

        IsCompleted = true;
        WinnerId = BotOne == Guid.Empty ? BotTwo : BotOne;
        IsDraw = false;
    }
}
