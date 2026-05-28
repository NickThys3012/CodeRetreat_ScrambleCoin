namespace ScrambleCoin.Application.Tournament.GetStandings;

/// <summary>
/// DTO containing the current group stage standings table.
/// </summary>
/// <param name="TournamentId">The tournament identifier.</param>
/// <param name="TournamentName">Human-readable tournament name.</param>
/// <param name="Status">Current tournament lifecycle status.</param>
/// <param name="Standings">Ordered standings (highest points first; coin score as tie-break).</param>
public sealed record TournamentStandingsDto(
    Guid TournamentId,
    string TournamentName,
    string Status,
    IReadOnlyList<StandingEntryDto> Standings);

/// <summary>Single row in the standings table.</summary>
/// <param name="Rank">1-based rank.</param>
/// <param name="BotId">Bot identifier.</param>
/// <param name="BotName">Bot display name.</param>
/// <param name="Played">Total matches played so far.</param>
/// <param name="Wins">Matches won.</param>
/// <param name="Draws">Matches drawn.</param>
/// <param name="Losses">Matches lost.</param>
/// <param name="Points">Total points (3W + 2D + 1L).</param>
/// <param name="TotalCoins">Tie-break: total coin score across all group games.</param>
public sealed record StandingEntryDto(
    int Rank,
    Guid BotId,
    string BotName,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int Points,
    int TotalCoins);
