namespace ScrambleCoin.Application.Tournament.GetBracket;

/// <summary>Full bracket state for a tournament.</summary>
/// <param name="TournamentId">The tournament identifier.</param>
/// <param name="TournamentName">Human-readable name.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="WinnerId">Bot ID of the overall tournament winner; <c>null</c> until Completed.</param>
/// <param name="GroupMatches">All round-robin group stage matches.</param>
/// <param name="KnockoutRounds">Knockout rounds, each containing their matches.</param>
public sealed record TournamentBracketDto(
    Guid TournamentId,
    string TournamentName,
    string Status,
    Guid? WinnerId,
    IReadOnlyList<GroupMatchDto> GroupMatches,
    IReadOnlyList<KnockoutRoundDto> KnockoutRounds);

/// <summary>DTO for a single group stage match.</summary>
/// <param name="MatchId">Unique match identifier.</param>
/// <param name="BotOne">Bot ID of the first participant.</param>
/// <param name="BotTwo">Bot ID of the second participant.</param>
/// <param name="GameId">The game assigned to this match; <c>null</c> if not yet assigned.</param>
/// <param name="BotOneToken">Auth token for BotOne; <c>null</c> if not yet assigned.</param>
/// <param name="BotTwoToken">Auth token for BotTwo; <c>null</c> if not yet assigned.</param>
/// <param name="IsCompleted">Whether this match has a recorded result.</param>
/// <param name="WinnerId">Bot ID of the winner; <c>null</c> if not complete or drawn.</param>
/// <param name="IsDraw">Whether the match ended in a draw.</param>
/// <param name="BotOneScore">Coin score for BotOne (0 if not complete).</param>
/// <param name="BotTwoScore">Coin score for BotTwo (0 if not complete).</param>
public sealed record GroupMatchDto(
    Guid MatchId,
    Guid BotOne,
    Guid BotTwo,
    Guid? GameId,
    Guid? BotOneToken,
    Guid? BotTwoToken,
    bool IsCompleted,
    Guid? WinnerId,
    bool IsDraw,
    int BotOneScore,
    int BotTwoScore);

/// <summary>One knockout round and all its matches.</summary>
/// <param name="Round">Round number (1 = first round).</param>
/// <param name="Matches">Matches in this round.</param>
public sealed record KnockoutRoundDto(
    int Round,
    IReadOnlyList<KnockoutMatchDto> Matches);

/// <summary>DTO for a single knockout match.</summary>
/// <param name="MatchId">Unique match identifier.</param>
/// <param name="Round">Knockout round number.</param>
/// <param name="Position">Position within the round (zero-based).</param>
/// <param name="BotOne">Bot ID of participant 1; <c>null</c> if TBD.</param>
/// <param name="BotTwo">Bot ID of participant 2; <c>null</c> if TBD.</param>
/// <param name="GameId">The game assigned to this match; <c>null</c> if not yet assigned.</param>
/// <param name="BotOneToken">Auth token for BotOne; <c>null</c> if not yet assigned.</param>
/// <param name="BotTwoToken">Auth token for BotTwo; <c>null</c> if not yet assigned.</param>
/// <param name="IsBye">True when one slot is a bye; the other bot advances automatically.</param>
/// <param name="IsCompleted">Whether this match has a recorded result.</param>
/// <param name="WinnerId">Bot ID of the winner; <c>null</c> if not complete.</param>
public sealed record KnockoutMatchDto(
    Guid MatchId,
    int Round,
    int Position,
    Guid? BotOne,
    Guid? BotTwo,
    Guid? GameId,
    Guid? BotOneToken,
    Guid? BotTwoToken,
    bool IsBye,
    bool IsCompleted,
    Guid? WinnerId);
