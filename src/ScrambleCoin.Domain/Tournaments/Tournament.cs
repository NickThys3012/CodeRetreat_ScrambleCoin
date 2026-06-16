using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.Tournaments;

/// <summary>
/// Aggregate root for a named tournament.
/// Manages the participant list, round-robin group stage schedule, and knockout bracket.
/// </summary>
public sealed class Tournament
{
    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Unique identifier for this tournament.</summary>
    public Guid Id { get; }

    /// <summary>Human-readable tournament name.</summary>
    public string Name { get; }

    /// <summary>Maximum number of bots that may participate.</summary>
    public int MaxParticipants { get; }

    /// <summary>
    /// Number of top-ranked group-stage bots that advance to the knockout stage.
    /// Default is 4.
    /// </summary>
    public int TopN { get; }

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>Current lifecycle status of this tournament.</summary>
    public TournamentStatus Status { get; private set; }

    /// <summary>Bot ID of the overall tournament winner; set when Status = Completed.</summary>
    public Guid? WinnerId { get; private set; }

    /// <summary>UTC timestamp when the tournament was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    // ── Participants ──────────────────────────────────────────────────────────

    private readonly List<TournamentParticipant> _participants = [];

    /// <summary>Registered participants, in registration order.</summary>
    public IReadOnlyList<TournamentParticipant> Participants => _participants.AsReadOnly();

    // ── Group stage ───────────────────────────────────────────────────────────

    private readonly List<GroupMatch> _groupMatches = [];

    /// <summary>All round-robin group matches (populated when the tournament starts).</summary>
    public IReadOnlyList<GroupMatch> GroupMatches => _groupMatches.AsReadOnly();

    // ── Knockout stage ────────────────────────────────────────────────────────

    private readonly List<KnockoutMatch> _knockoutMatches = [];

    /// <summary>All knockout bracket matches (populated when the group stage completes).</summary>
    public IReadOnlyList<KnockoutMatch> KnockoutMatches => _knockoutMatches.AsReadOnly();

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new tournament in <see cref="TournamentStatus.Pending"/> state.
    /// </summary>
    public Tournament(Guid id, string name, int maxParticipants, int topN, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tournament name must not be empty.");

        if (maxParticipants < 2)
            throw new DomainException("A tournament requires at least 2 participants.");

        if (topN < 2)
            throw new DomainException("TopN (bots advancing to knockout) must be at least 2.");

        if (topN > maxParticipants)
            throw new DomainException($"TopN ({topN}) cannot exceed MaxParticipants ({maxParticipants}).");

        Id = id;
        Name = name;
        MaxParticipants = maxParticipants;
        TopN = topN;
        Status = TournamentStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    // ── Participant management ────────────────────────────────────────────────

    /// <summary>
    /// Registers a bot as a participant.
    /// </summary>
    /// <exception cref="TournamentInvalidStateException">
    /// Thrown when the tournament is not in <see cref="TournamentStatus.Pending"/> state,
    /// or when the maximum participant count is already reached.
    /// </exception>
    /// <exception cref="DomainException">Thrown when the bot is already registered.</exception>
    public void AddParticipant(Guid botId, string botName, IReadOnlyList<string> lineup)
    {
        if (Status != TournamentStatus.Pending)
            throw new TournamentInvalidStateException(
                $"Participants can only be added while the tournament is in {TournamentStatus.Pending} state. Current status: {Status}.");

        if (_participants.Count >= MaxParticipants)
            throw new TournamentInvalidStateException(
                $"Tournament '{Name}' already has the maximum of {MaxParticipants} participants.");

        if (_participants.Any(p => p.BotId == botId))
            throw new DomainException($"Bot {botId} is already registered in tournament '{Name}'.");

        _participants.Add(new TournamentParticipant(botId, botName, lineup));
    }

    // ── Start (group stage scheduling) ────────────────────────────────────────

    /// <summary>
    /// Locks participants and generates the round-robin group stage schedule.
    /// Transitions status to <see cref="TournamentStatus.GroupStage"/>.
    /// </summary>
    /// <returns>The list of group matches to be played (in order).</returns>
    /// <exception cref="TournamentInvalidStateException">
    /// Thrown when the tournament is not <see cref="TournamentStatus.Pending"/> or has fewer than 2 participants.
    /// </exception>
    public IReadOnlyList<GroupMatch> Start()
    {
        if (Status != TournamentStatus.Pending)
            throw new TournamentInvalidStateException(
                $"Tournament can only be started from {TournamentStatus.Pending} state. Current status: {Status}.");

        if (_participants.Count < 2)
            throw new TournamentInvalidStateException(
                "Tournament requires at least 2 participants before it can be started.");

        _groupMatches.AddRange(GenerateRoundRobinSchedule());
        Status = TournamentStatus.GroupStage;

        return _groupMatches.AsReadOnly();
    }

    // ── Group result recording ────────────────────────────────────────────────

    /// <summary>
    /// Records the result of a completed group match.
    /// </summary>
    /// <param name="matchId">The ID of the group match to update.</param>
    /// <param name="winnerId">Bot ID of the winner, or <c>null</c> for a draw.</param>
    /// <param name="isDraw">True when the underlying game ended in a draw.</param>
    /// <param name="botOneScore">Coin score of the match's BotOne.</param>
    /// <param name="botTwoScore">Coin score of the match's BotTwo.</param>
    /// <exception cref="DomainException">Thrown when the match is not found.</exception>
    public void RecordGroupResult(Guid matchId, Guid? winnerId, bool isDraw, int botOneScore, int botTwoScore)
    {
        var match = _groupMatches.FirstOrDefault(m => m.Id == matchId)
            ?? throw new DomainException($"Group match {matchId} was not found in tournament {Id}.");

        if (match.IsCompleted)
            return; // idempotent

        // Translate game player IDs back to bot IDs
        Guid? botWinnerId = null;
        if (winnerId.HasValue)
        {
            botWinnerId = match.BotOnePlayerId == winnerId ? match.BotOne
                        : match.BotTwoPlayerId == winnerId ? match.BotTwo
                        : null;
        }

        match.RecordResult(botWinnerId, isDraw, botOneScore, botTwoScore);
    }

    // ── Knockout stage generation ─────────────────────────────────────────────

    /// <summary>
    /// Checks whether all group stage matches have been completed.
    /// </summary>
    public bool IsGroupStageComplete() =>
        Status == TournamentStatus.GroupStage && _groupMatches.All(m => m.IsCompleted);

    /// <summary>
    /// Generates the single-elimination knockout bracket from the current group standings.
    /// Transitions status to <see cref="TournamentStatus.KnockoutStage"/>.
    /// </summary>
    /// <returns>The first round of knockout matches to be played.</returns>
    /// <exception cref="TournamentInvalidStateException">
    /// Thrown when the group stage is not yet complete.
    /// </exception>
    public IReadOnlyList<KnockoutMatch> AdvanceToKnockout()
    {
        if (Status != TournamentStatus.GroupStage)
            throw new TournamentInvalidStateException(
                $"Can only advance to knockout from {TournamentStatus.GroupStage} state. Current status: {Status}.");

        if (!IsGroupStageComplete())
            throw new TournamentInvalidStateException(
                "Cannot advance to knockout: not all group stage matches have been completed.");

        var standings = ComputeStandings();
        var ranked = standings.OrderByDescending(s => s.Points)
                              .ThenByDescending(s => s.TotalCoins)
                              .Select(s => s.BotId)
                              .Take(TopN)
                              .ToList();

        _knockoutMatches.AddRange(GenerateKnockoutBracket(ranked));
        Status = TournamentStatus.KnockoutStage;

        // Resolve any first-round byes immediately and propagate winners upward
        var firstRound = _knockoutMatches.Where(m => m.Round == 1).ToList();
        var maxRound = _knockoutMatches.Max(m => m.Round);
        foreach (var match in firstRound.Where(m => m.IsBye))
        {
            match.ResolveBye();
            PropagateByeWinner(match, maxRound);
        }

        return firstRound.AsReadOnly();
    }

    // ── Knockout result recording ─────────────────────────────────────────────

    /// <summary>
    /// Propagates a resolved bye's winner into the appropriate slot of the next-round match.
    /// If the propagated value is <see cref="Guid.Empty"/> (double-bye), the next match also
    /// becomes a bye and is resolved immediately, cascading upward as needed.
    /// </summary>
    private void PropagateByeWinner(KnockoutMatch match, int maxRound)
    {
        if (match.Round >= maxRound || !match.WinnerId.HasValue)
            return;

        var nextRound    = match.Round + 1;
        var nextPosition = match.Position / 2;
        var nextSlot     = (match.Position % 2 == 0) ? 1 : 2;
        var nextMatch    = _knockoutMatches.FirstOrDefault(m => m.Round == nextRound && m.Position == nextPosition);
        if (nextMatch is null)
            return;

        nextMatch.SetParticipant(nextSlot, match.WinnerId.Value);

        // If propagating a double-bye created another bye in the next round (both slots known,
        // at least one is Guid.Empty), resolve it immediately and keep propagating.
        if (nextMatch is not { IsBye: true, BotOne: not null, BotTwo: not null, IsCompleted: false })
        {
            return;
        }
        nextMatch.ResolveBye();
        PropagateByeWinner(nextMatch, maxRound);
    }

    /// <summary>
    /// If this is the final match, transitions to <see cref="TournamentStatus.Completed"/>.
    /// </summary>
    /// <returns>
    /// The next-round match that was updated (or <c>null</c> if this was the final).
    /// </returns>
    public KnockoutMatch? RecordKnockoutResult(Guid matchId, Guid? gameWinnerPlayerId, bool isDraw)
    {
        var match = _knockoutMatches.FirstOrDefault(m => m.Id == matchId)
            ?? throw new DomainException($"Knockout match {matchId} was not found in tournament {Id}.");

        if (match.IsCompleted)
            return null; // idempotent

        // Translate game player ID → bot ID
        Guid? botWinnerId = null;
        if (gameWinnerPlayerId.HasValue)
        {
            botWinnerId = match.BotOnePlayerId == gameWinnerPlayerId ? match.BotOne
                        : match.BotTwoPlayerId == gameWinnerPlayerId ? match.BotTwo
                        : null;

            if (botWinnerId is null)
                throw new DomainException(
                    $"Winner player ID {gameWinnerPlayerId} does not belong to knockout match {matchId}.");
        }

        // Deterministic tie-break for genuine draws: the bot assigned to the BotOne slot
        // advances. BotOne == higher seed only in round 1 (by bracket construction); in
        // later rounds BotOne is whichever bot won the even-positioned upstream match.
        // This is intentional — seed rank is not propagated through the bracket.
        if (isDraw)
            botWinnerId = match.BotOne;

        match.RecordResult(botWinnerId, isDraw);

        // Find the final round number
        var maxRound = _knockoutMatches.Max(m => m.Round);

        if (match.Round == maxRound)
        {
            // This was the final — tournament is complete
            WinnerId = botWinnerId;
            Status = TournamentStatus.Completed;
            return null;
        }

        // Populate the next-round match
        var nextRound = match.Round + 1;
        var nextPosition = match.Position / 2;
        var nextSlot = (match.Position % 2 == 0) ? 1 : 2;

        var nextMatch = _knockoutMatches.FirstOrDefault(m => m.Round == nextRound && m.Position == nextPosition);
        if (nextMatch is not null && botWinnerId.HasValue)
            nextMatch.SetParticipant(nextSlot, botWinnerId.Value);

        return nextMatch;
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cancels the tournament. Allowed in any non-terminal state.
    /// </summary>
    /// <exception cref="TournamentInvalidStateException">
    /// Thrown when the tournament is already <see cref="TournamentStatus.Completed"/> or <see cref="TournamentStatus.Cancelled"/>.
    /// </exception>
    public void Cancel()
    {
        if (Status is TournamentStatus.Completed or TournamentStatus.Cancelled)
            throw new TournamentInvalidStateException(
                $"Cannot cancel a tournament that is already {Status}.");

        Status = TournamentStatus.Cancelled;
    }

    // ── Standings ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the current group stage standings from completed group matches.
    /// Points: 3 for win, 2 for draw, 1 for loss.
    /// Tie-break: total coin score across all completed group games.
    /// </summary>
    public IReadOnlyList<TournamentStanding> ComputeStandings()
    {
        var standings = _participants.ToDictionary(
            p => p.BotId,
            p => new StandingAccumulator(p.BotId, p.BotName));

        foreach (var match in _groupMatches.Where(m => m.IsCompleted))
        {
            var accOne = standings[match.BotOne];
            var accTwo = standings[match.BotTwo];

            accOne.TotalCoins += match.BotOneScore;
            accTwo.TotalCoins += match.BotTwoScore;

            if (match.IsDraw)
            {
                accOne.Draws++;
                accTwo.Draws++;
                accOne.Points += 2;
                accTwo.Points += 2;
            }
            else if (match.WinnerId == match.BotOne)
            {
                accOne.Wins++;
                accTwo.Losses++;
                accOne.Points += 3;
                accTwo.Points += 1;
            }
            else if (match.WinnerId == match.BotTwo)
            {
                accTwo.Wins++;
                accOne.Losses++;
                accTwo.Points += 3;
                accOne.Points += 1;
            }
        }

        return standings.Values
            .Select(a => new TournamentStanding(a.BotId, a.BotName, a.Wins, a.Draws, a.Losses, a.Points, a.TotalCoins))
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.TotalCoins)
            .ToList()
            .AsReadOnly();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Generates a round-robin group stage schedule using the polygon rotation algorithm.
    /// If the participant count is odd, a "bye" is added so every bot sits out exactly once.
    /// </summary>
    private List<GroupMatch> GenerateRoundRobinSchedule()
    {
        var bots = _participants.Select(p => p.BotId).ToList();

        // With an odd number, add a sentinel "bye" so the total count is even.
        var hasBye = bots.Count % 2 != 0;
        if (hasBye)
            bots.Add(Guid.Empty); // Guid.Empty = bye slot

        var n = bots.Count;
        var rounds = n - 1;
        var matchesPerRound = n / 2;

        var matches = new List<GroupMatch>();
        var rotation = bots.ToList();

        for (var round = 0; round < rounds; round++)
        {
            for (var i = 0; i < matchesPerRound; i++)
            {
                var botOne = rotation[i];
                var botTwo = rotation[n - 1 - i];

                // Skip bye matches (one participant is the sentinel)
                if (botOne != Guid.Empty && botTwo != Guid.Empty)
                    matches.Add(new GroupMatch(Guid.NewGuid(), botOne, botTwo));
            }

            // Rotate: fix position 0, move the last element to position 1
            var last = rotation[n - 1];
            rotation.RemoveAt(n - 1);
            rotation.Insert(1, last);
        }

        return matches;
    }

    /// <summary>
    /// Generates a single-elimination knockout bracket from a ranked list of bots.
    /// The bracket is padded to the next power of two with bye slots.
    /// Standard seeding: seed 1 vs last, seed 2 vs second-to-last, etc.
    /// </summary>
    private static List<KnockoutMatch> GenerateKnockoutBracket(IReadOnlyList<Guid> rankedBots)
    {
        var participants = rankedBots.ToList();

        // Pad to next power of 2 with bye sentinels
        var bracketSize = NextPowerOf2(participants.Count);
        while (participants.Count < bracketSize)
            participants.Add(Guid.Empty);

        var allMatches = new List<KnockoutMatch>();
        var round = 1;
        var slots = bracketSize;

        // Round 1: seed 1 vs last, 2 vs second-to-last (standard bracket seeding)
        var roundBots = participants.ToList();
        while (roundBots.Count > 1)
        {
            var matchCount = roundBots.Count / 2;
            for (var i = 0; i < matchCount; i++)
            {
                // Preserve Guid.Empty as-is so KnockoutMatch.IsBye (which checks == Guid.Empty) works correctly.
                Guid? botOne = roundBots[i];
                Guid? botTwo = roundBots[slots - 1 - i];

                // For round 1, use standard seeding pairing (top vs bottom)
                // For later rounds, participants are TBD (null)
                allMatches.Add(round == 1 ? new KnockoutMatch(Guid.NewGuid(), round, i, botOne, botTwo) : new KnockoutMatch(Guid.NewGuid(), round, i, null, null));
            }

            // The next round has half the slots
            slots /= 2;
            roundBots = Enumerable.Repeat(Guid.Empty, slots).ToList();
            round++;
        }

        return allMatches;
    }

    private static int NextPowerOf2(int n)
    {
        if (n <= 1) return 1;
        var p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    // ── Mutable accumulator (private helper) ─────────────────────────────────

    private sealed class StandingAccumulator
    {
        public Guid BotId { get; }
        public string BotName { get; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int Points { get; set; }
        public int TotalCoins { get; set; }

        public StandingAccumulator(Guid botId, string botName)
        {
            BotId = botId;
            BotName = botName;
        }
    }
}
