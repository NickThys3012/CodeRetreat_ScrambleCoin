namespace ScrambleCoin.Domain.Tournaments;

/// <summary>
/// Represents a match between two bots in the round-robin group stage.
/// </summary>
public sealed class GroupMatch
{
    /// <summary>Unique identifier for this match.</summary>
    public Guid Id { get; }

    /// <summary>Bot ID of the first participant.</summary>
    public Guid BotOne { get; }

    /// <summary>Bot ID of the second participant.</summary>
    public Guid BotTwo { get; }

    /// <summary>
    /// The game created for this match; <c>null</c> until the game is assigned.
    /// </summary>
    public Guid? GameId { get; private set; }

    /// <summary>
    /// Player-slot ID for BotOne inside the game (<c>PlayerOne</c> or <c>PlayerTwo</c> of the game).
    /// <c>null</c> until a game is assigned.
    /// </summary>
    public Guid? BotOnePlayerId { get; private set; }

    /// <summary>
    /// Player-slot ID for BotTwo inside the game.
    /// <c>null</c> until a game is assigned.
    /// </summary>
    public Guid? BotTwoPlayerId { get; private set; }

    /// <summary>
    /// Authentication token for BotOne to use when interacting with the game.
    /// <c>null</c> until a game is assigned.
    /// </summary>
    public Guid? BotOneToken { get; private set; }

    /// <summary>
    /// Authentication token for BotTwo to use when interacting with the game.
    /// <c>null</c> until a game is assigned.
    /// </summary>
    public Guid? BotTwoToken { get; private set; }

    /// <summary>Whether this match has been completed.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Bot ID of the winner; <c>null</c> if the match is not complete or was a draw.
    /// </summary>
    public Guid? WinnerId { get; private set; }

    /// <summary>Whether the completed match was a draw.</summary>
    public bool IsDraw { get; private set; }

    /// <summary>Coin score for BotOne (from the underlying game).</summary>
    public int BotOneScore { get; private set; }

    /// <summary>Coin score for BotTwo (from the underlying game).</summary>
    public int BotTwoScore { get; private set; }

    public GroupMatch(Guid id, Guid botOne, Guid botTwo)
    {
        Id = id;
        BotOne = botOne;
        BotTwo = botTwo;
    }

    /// <summary>
    /// Assigns a game to this group match and stores the per-game player slot IDs and tokens.
    /// </summary>
    public void AssignGame(Guid gameId, Guid botOnePlayerId, Guid botOneToken, Guid botTwoPlayerId, Guid botTwoToken)
    {
        if (IsCompleted)
            throw new InvalidOperationException($"Cannot assign a game to completed group match {Id}.");

        GameId = gameId;
        BotOnePlayerId = botOnePlayerId;
        BotOneToken = botOneToken;
        BotTwoPlayerId = botTwoPlayerId;
        BotTwoToken = botTwoToken;
    }

    /// <summary>Records the result of this group match.</summary>
    /// <param name="winnerId">Bot ID of the winner, or <c>null</c> for a draw.</param>
    /// <param name="isDraw">Whether the match ended in a draw.</param>
    /// <param name="botOneScore">Coin score of BotOne.</param>
    /// <param name="botTwoScore">Coin score of BotTwo.</param>
    public void RecordResult(Guid? winnerId, bool isDraw, int botOneScore, int botTwoScore)
    {
        IsCompleted = true;
        WinnerId = winnerId;
        IsDraw = isDraw;
        BotOneScore = botOneScore;
        BotTwoScore = botTwoScore;
    }
}
