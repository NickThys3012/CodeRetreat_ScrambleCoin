using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Application.Tournament;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Tournaments;
using ScrambleCoin.Infrastructure.Persistence.Records;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed implementation of <see cref="ITournamentRepository"/>.
/// Serializes the <see cref="Tournament"/> aggregate to/from <see cref="TournamentRecord"/>.
/// Complex nested data is stored as JSON columns. Reflection is used to restore private fields
/// on hydration (same pattern as <see cref="GameRepository"/>).
/// </summary>
public sealed class TournamentRepository : ITournamentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ScrambleCoinDbContext _context;

    public TournamentRepository(ScrambleCoinDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<Tournament> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.Tournaments.FindAsync([id], cancellationToken)
            ?? throw new TournamentNotFoundException(id);

        return Hydrate(record);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        var record = Dehydrate(tournament);

        var existing = await _context.Tournaments.FindAsync([tournament.Id], cancellationToken);
        if (existing is null)
        {
            _context.Tournaments.Add(record);
        }
        else
        {
            existing.Name = record.Name;
            existing.MaxParticipants = record.MaxParticipants;
            existing.TopN = record.TopN;
            existing.Status = record.Status;
            existing.WinnerId = record.WinnerId;
            existing.ParticipantsJson = record.ParticipantsJson;
            existing.GroupMatchesJson = record.GroupMatchesJson;
            existing.KnockoutMatchesJson = record.KnockoutMatchesJson;
        }

        // No SaveChangesAsync — the caller is responsible for committing via IUnitOfWork.SaveChangesAsync.
    }

    /// <inheritdoc/>
    public async Task<TournamentBotInfo?> GetBotInfoByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        // Load all tournament records and scan JSON columns for the matching game ID.
        // Tournament data is small enough (one row per tournament) that a full scan is acceptable.
        var records = await _context.Tournaments.ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            var participants = JsonSerializer.Deserialize<List<TournamentParticipantDto>>(
                record.ParticipantsJson, JsonOptions) ?? [];

            // ── Search group matches ───────────────────────────────────────────
            var groupMatches = JsonSerializer.Deserialize<List<GroupMatchDto>>(
                record.GroupMatchesJson, JsonOptions) ?? [];

            var gm = groupMatches.FirstOrDefault(m => m.GameId == gameId);
            if (gm is not null)
            {
                return new TournamentBotInfo(
                    BotOneId:       gm.BotOne,
                    BotOneName:     participants.FirstOrDefault(p => p.BotId == gm.BotOne)?.BotName
                                        ?? $"Bot-{gm.BotOne:N}"[..13],
                    BotOnePlayerId: gm.BotOnePlayerId,
                    BotTwoId:       gm.BotTwo,
                    BotTwoName:     participants.FirstOrDefault(p => p.BotId == gm.BotTwo)?.BotName
                                        ?? $"Bot-{gm.BotTwo:N}"[..13],
                    BotTwoPlayerId: gm.BotTwoPlayerId);
            }

            // ── Search knockout matches ────────────────────────────────────────
            var knockoutMatches = JsonSerializer.Deserialize<List<KnockoutMatchDto>>(
                record.KnockoutMatchesJson, JsonOptions) ?? [];

            var km = knockoutMatches.FirstOrDefault(m => m.GameId == gameId);
            if (km is { BotOne: not null, BotTwo: not null })
            {
                return new TournamentBotInfo(
                    BotOneId:       km.BotOne.Value,
                    BotOneName:     participants.FirstOrDefault(p => p.BotId == km.BotOne)?.BotName
                                        ?? $"Bot-{km.BotOne.Value:N}"[..13],
                    BotOnePlayerId: km.BotOnePlayerId,
                    BotTwoId:       km.BotTwo.Value,
                    BotTwoName:     participants.FirstOrDefault(p => p.BotId == km.BotTwo)?.BotName
                                        ?? $"Bot-{km.BotTwo.Value:N}"[..13],
                    BotTwoPlayerId: km.BotTwoPlayerId);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await _context.Tournaments.AsNoTracking().ToListAsync(cancellationToken);
        return records.Select(Hydrate).ToList().AsReadOnly();
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    private static TournamentRecord Dehydrate(Tournament tournament)
    {
        var participants = tournament.Participants.Select(p => new TournamentParticipantDto(
            p.BotId,
            p.BotName,
            p.Lineup.ToList())).ToList();

        var groupMatches = tournament.GroupMatches.Select(m => new GroupMatchDto(
            m.Id,
            m.BotOne,
            m.BotTwo,
            m.GameId,
            m.BotOnePlayerId,
            m.BotOneToken,
            m.BotTwoPlayerId,
            m.BotTwoToken,
            m.IsCompleted,
            m.WinnerId,
            m.IsDraw,
            m.BotOneScore,
            m.BotTwoScore)).ToList();

        var knockoutMatches = tournament.KnockoutMatches.Select(m => new KnockoutMatchDto(
            m.Id,
            m.Round,
            m.Position,
            m.BotOne,
            m.BotTwo,
            m.GameId,
            m.BotOnePlayerId,
            m.BotOneToken,
            m.BotTwoPlayerId,
            m.BotTwoToken,
            m.IsCompleted,
            m.WinnerId,
            m.IsDraw)).ToList();

        return new TournamentRecord
        {
            Id = tournament.Id,
            Name = tournament.Name,
            MaxParticipants = tournament.MaxParticipants,
            TopN = tournament.TopN,
            Status = (int)tournament.Status,
            WinnerId = tournament.WinnerId,
            CreatedAtUtc = tournament.CreatedAtUtc,
            ParticipantsJson = JsonSerializer.Serialize(participants, JsonOptions),
            GroupMatchesJson = JsonSerializer.Serialize(groupMatches, JsonOptions),
            KnockoutMatchesJson = JsonSerializer.Serialize(knockoutMatches, JsonOptions)
        };
    }

    private static Tournament Hydrate(TournamentRecord record)
    {
        // Create the aggregate via its public constructor (Status will be Pending initially)
        var tournament = new Tournament(
            id: record.Id,
            name: record.Name,
            maxParticipants: record.MaxParticipants,
            topN: record.TopN,
            createdAtUtc: record.CreatedAtUtc);

        // ── Restore private backing fields via reflection ──────────────────────

        var type = typeof(Tournament);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        // Status
        type.GetProperty(nameof(Tournament.Status))!
            .SetValue(tournament, (TournamentStatus)record.Status);

        // WinnerId
        type.GetProperty(nameof(Tournament.WinnerId))!
            .SetValue(tournament, record.WinnerId);

        // Participants list
        var participantsField = type.GetField("_participants", flags)!;
        var participantsList = (List<TournamentParticipant>)participantsField.GetValue(tournament)!;
        var participantDtos = JsonSerializer.Deserialize<List<TournamentParticipantDto>>(
            record.ParticipantsJson, JsonOptions) ?? [];

        foreach (var p in participantDtos)
            participantsList.Add(new TournamentParticipant(p.BotId, p.BotName, p.Lineup));

        // Group matches list
        var groupMatchesField = type.GetField("_groupMatches", flags)!;
        var groupMatchesList = (List<GroupMatch>)groupMatchesField.GetValue(tournament)!;
        var groupMatchDtos = JsonSerializer.Deserialize<List<GroupMatchDto>>(
            record.GroupMatchesJson, JsonOptions) ?? [];

        foreach (var dto in groupMatchDtos)
        {
            var match = new GroupMatch(dto.Id, dto.BotOne, dto.BotTwo);

            if (dto is { GameId: not null, BotOnePlayerId: not null, BotOneToken: not null, BotTwoPlayerId: not null, BotTwoToken: not null })
            {
                match.AssignGame(dto.GameId.Value, dto.BotOnePlayerId.Value, dto.BotOneToken.Value,
                                 dto.BotTwoPlayerId.Value, dto.BotTwoToken.Value);
            }

            if (dto.IsCompleted)
                match.RecordResult(dto.WinnerId, dto.IsDraw, dto.BotOneScore, dto.BotTwoScore);

            groupMatchesList.Add(match);
        }

        // Knockout matches list
        var knockoutMatchesField = type.GetField("_knockoutMatches", flags)!;
        var knockoutMatchesList = (List<KnockoutMatch>)knockoutMatchesField.GetValue(tournament)!;
        var knockoutMatchDtos = JsonSerializer.Deserialize<List<KnockoutMatchDto>>(
            record.KnockoutMatchesJson, JsonOptions) ?? [];

        foreach (var dto in knockoutMatchDtos)
        {
            var match = new KnockoutMatch(dto.Id, dto.Round, dto.Position, dto.BotOne, dto.BotTwo);

            if (dto is { GameId: not null, BotOnePlayerId: not null, BotOneToken: not null, BotTwoPlayerId: not null, BotTwoToken: not null })
            {
                match.AssignGame(dto.GameId.Value, dto.BotOnePlayerId.Value, dto.BotOneToken.Value,
                                 dto.BotTwoPlayerId.Value, dto.BotTwoToken.Value);
            }

            if (dto.IsCompleted)
                match.RecordResult(dto.WinnerId, dto.IsDraw);

            knockoutMatchesList.Add(match);
        }

        return tournament;
    }
}
