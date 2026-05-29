using System.ComponentModel.DataAnnotations;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Tournaments;

namespace ScrambleCoin.Infrastructure.Persistence.Records;

/// <summary>
/// EF Core persistence POCO for the <see cref="Tournament"/> aggregate.
/// Complex nested data (participants, group matches, knockout matches) is stored as JSON.
/// </summary>
public sealed class TournamentRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaxParticipants { get; set; }
    public int TopN { get; set; }

    /// <summary>Persisted value of <see cref="TournamentStatus"/> cast to int.</summary>
    public int Status { get; set; }

    public Guid? WinnerId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// EF Core optimistic concurrency token. Prevents concurrent GET /bracket requests from
    /// both advancing the tournament status and creating duplicate knockout games.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // ── JSON columns ──────────────────────────────────────────────────────────

    /// <summary>JSON: List of <see cref="TournamentParticipantDto"/>.</summary>
    public string ParticipantsJson { get; set; } = "[]";

    /// <summary>JSON: List of <see cref="GroupMatchDto"/>.</summary>
    public string GroupMatchesJson { get; set; } = "[]";

    /// <summary>JSON: List of <see cref="KnockoutMatchDto"/>.</summary>
    public string KnockoutMatchesJson { get; set; } = "[]";
}

// ── Internal JSON transfer objects ────────────────────────────────────────────

internal sealed record TournamentParticipantDto(
    Guid BotId,
    string BotName,
    List<string> Lineup);

internal sealed record GroupMatchDto(
    Guid Id,
    Guid BotOne,
    Guid BotTwo,
    Guid? GameId,
    Guid? BotOnePlayerId,
    Guid? BotOneToken,
    Guid? BotTwoPlayerId,
    Guid? BotTwoToken,
    bool IsCompleted,
    Guid? WinnerId,
    bool IsDraw,
    int BotOneScore,
    int BotTwoScore);

internal sealed record KnockoutMatchDto(
    Guid Id,
    int Round,
    int Position,
    Guid? BotOne,
    Guid? BotTwo,
    Guid? GameId,
    Guid? BotOnePlayerId,
    Guid? BotOneToken,
    Guid? BotTwoPlayerId,
    Guid? BotTwoToken,
    bool IsCompleted,
    Guid? WinnerId,
    bool IsDraw);
