using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Entities;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tournament.GetBracket;

/// <summary>
/// Handles <see cref="GetTournamentBracketQuery"/>:
/// lazily syncs both group and knockout game results, advances the tournament stage when
/// all matches in a round complete, and returns the full bracket DTO.
/// </summary>
public sealed class GetTournamentBracketQueryHandler : IRequestHandler<GetTournamentBracketQuery, TournamentBracketDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetTournamentBracketQueryHandler> _logger;

    public GetTournamentBracketQueryHandler(
        ITournamentRepository tournamentRepository,
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        IUnitOfWork unitOfWork,
        ILogger<GetTournamentBracketQueryHandler> logger)
    {
        _tournamentRepository = tournamentRepository;
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TournamentBracketDto> Handle(GetTournamentBracketQuery request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);

        var dirty = false;

        // ── Group stage sync ──────────────────────────────────────────────────
        if (tournament.Status == TournamentStatus.GroupStage)
        {
            dirty = await SyncGroupResultsAsync(tournament, cancellationToken);

            if (tournament.IsGroupStageComplete())
            {
                _logger.LogInformation(
                    "Tournament {TournamentId}: all group games complete. Advancing to knockout.",
                    tournament.Id);

                tournament.AdvanceToKnockout();
                dirty = true;

                // Create games for round 1 knockout matches
                await CreateKnockoutGamesForCurrentRoundAsync(tournament, 1, cancellationToken);
            }
        }

        // ── Knockout stage sync ───────────────────────────────────────────────
        if (tournament.Status == TournamentStatus.KnockoutStage)
        {
            var knockoutDirty = await SyncKnockoutResultsAsync(tournament, cancellationToken);
            dirty = dirty || knockoutDirty;
        }

        if (dirty)
        {
            try
            {
                await _tournamentRepository.SaveAsync(tournament, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // A concurrent request already committed the same state transition.
                // Reload the now-current tournament and return it without retrying.
                _logger.LogWarning(
                    "Tournament {TournamentId}: concurrency conflict on bracket sync. Reloading current state.",
                    tournament.Id);
                tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
            }
        }

        return BuildDto(tournament);
    }

    // ── Sync helpers ──────────────────────────────────────────────────────────

    private async Task<bool> SyncGroupResultsAsync(DomainTournament tournament, CancellationToken ct)
    {
        var dirty = false;

        foreach (var match in tournament.GroupMatches.Where(m => m is { IsCompleted: false, GameId: not null }))
        {
            Game game;
            try { game = await _gameRepository.GetByIdAsync(match.GameId!.Value, ct); }
            catch (Domain.Exceptions.GameNotFoundException) { continue; }

            if (game.Status != GameStatus.Finished) continue;

            game.Scores.TryGetValue(game.PlayerOne, out var scoreOne);
            game.Scores.TryGetValue(game.PlayerTwo, out var scoreTwo);

            var isDraw = scoreOne == scoreTwo;
            Guid? gameWinnerId = isDraw ? null : (scoreOne > scoreTwo ? game.PlayerOne : game.PlayerTwo);

            var botOneScore = match.BotOnePlayerId == game.PlayerOne ? scoreOne : scoreTwo;
            var botTwoScore = match.BotTwoPlayerId == game.PlayerTwo ? scoreTwo : scoreOne;

            tournament.RecordGroupResult(match.Id, gameWinnerId, isDraw, botOneScore, botTwoScore);
            dirty = true;
        }

        return dirty;
    }

    private async Task<bool> SyncKnockoutResultsAsync(DomainTournament tournament, CancellationToken ct)
    {
        var dirty = false;

        // Process rounds in order so that next-round participants populate before we create their games
        var maxRound = tournament.KnockoutMatches.Count > 0
            ? tournament.KnockoutMatches.Max(m => m.Round)
            : 0;

        for (var round = 1; round <= maxRound; round++)
        {
            var roundMatches = tournament.KnockoutMatches
                .Where(m => m.Round == round)
                .ToList();

            foreach (var match in roundMatches.Where(m => m is { IsCompleted: false, GameId: not null }))
            {
                Game game;
                try { game = await _gameRepository.GetByIdAsync(match.GameId!.Value, ct); }
                catch (Domain.Exceptions.GameNotFoundException) { continue; }

                if (game.Status != GameStatus.Finished) continue;

                game.Scores.TryGetValue(game.PlayerOne, out var scoreOne);
                game.Scores.TryGetValue(game.PlayerTwo, out var scoreTwo);

                var isDraw = scoreOne == scoreTwo;
                Guid? gameWinnerId = isDraw ? null : (scoreOne > scoreTwo ? game.PlayerOne : game.PlayerTwo);

                tournament.RecordKnockoutResult(match.Id, gameWinnerId, isDraw);

                _logger.LogInformation(
                    "Tournament {TournamentId}: knockout match {MatchId} R{Round} result synced. Winner={Winner}",
                    tournament.Id, match.Id, round, gameWinnerId?.ToString() ?? "draw");

                dirty = true;
            }

            // If all matches in this round are now complete, create games for the next round
            var roundComplete = roundMatches.All(m => m.IsCompleted);
            if (roundComplete && round < maxRound)
            {
                await CreateKnockoutGamesForCurrentRoundAsync(tournament, round + 1, ct);
                dirty = true;
            }
        }

        return dirty;
    }

    private async Task CreateKnockoutGamesForCurrentRoundAsync(
        DomainTournament tournament,
        int round,
        CancellationToken ct)
    {
        var participantMap = tournament.Participants.ToDictionary(p => p.BotId);

        foreach (var match in tournament.KnockoutMatches
            .Where(m => m.Round == round && m is { IsCompleted: false, GameId: null, BotOne: not null, BotTwo: not null }
                && m.BotOne.Value != Guid.Empty && m.BotTwo.Value != Guid.Empty))
        {
            if (!participantMap.TryGetValue(match.BotOne!.Value, out var partOne) ||
                !participantMap.TryGetValue(match.BotTwo!.Value, out var partTwo))
                continue;

            var gameId = Guid.NewGuid();
            var botOnePlayerId = Guid.NewGuid();
            var botOneToken = Guid.NewGuid();
            var botTwoPlayerId = Guid.NewGuid();
            var botTwoToken = Guid.NewGuid();

            match.AssignGame(gameId, botOnePlayerId, botOneToken, botTwoPlayerId, botTwoToken);

            var board = new Board();
            var game = new Game(gameId, botOnePlayerId, botTwoPlayerId, board);

            var lineupOne = BuildLineup(botOnePlayerId, partOne.Lineup);
            var lineupTwo = BuildLineup(botTwoPlayerId, partTwo.Lineup);
            game.SetLineup(botOnePlayerId, lineupOne);
            game.SetLineup(botTwoPlayerId, lineupTwo);
            game.Start();
            game.ClearDomainEvents();

            await _gameRepository.StageAsync(game, ct);

            var regOne = new Domain.BotRegistrations.BotRegistration(botOneToken, botOnePlayerId, gameId);
            var regTwo = new Domain.BotRegistrations.BotRegistration(botTwoToken, botTwoPlayerId, gameId);
            await _botRegistrationRepository.StageAsync(regOne, ct);
            await _botRegistrationRepository.StageAsync(regTwo, ct);

            _logger.LogInformation(
                "Tournament {TournamentId}: knockout R{Round} match {MatchId} game {GameId} created.",
                tournament.Id, round, match.Id, gameId);
        }
    }

    private static Domain.ValueObjects.Lineup BuildLineup(Guid playerId, IReadOnlyList<string> pieceNames)
    {
        var pieces = pieceNames.Select(name => Domain.Factories.PieceFactory.Create(name, playerId)).ToList();
        return new Domain.ValueObjects.Lineup(pieces);
    }

    // ── DTO builder ───────────────────────────────────────────────────────────

    private static TournamentBracketDto BuildDto(DomainTournament tournament)
    {
        var participants = tournament.Participants
            .Select(p => new ParticipantDto(p.BotId, p.BotName))
            .ToList()
            .AsReadOnly();

        var groupDtos = tournament.GroupMatches.Select(m => new GroupMatchDto(
            MatchId: m.Id,
            BotOne: m.BotOne,
            BotTwo: m.BotTwo,
            GameId: m.GameId,
            IsCompleted: m.IsCompleted,
            WinnerId: m.WinnerId,
            IsDraw: m.IsDraw,
            BotOneScore: m.BotOneScore,
            BotTwoScore: m.BotTwoScore)).ToList();

        var knockoutRounds = tournament.KnockoutMatches
            .GroupBy(m => m.Round)
            .OrderBy(g => g.Key)
            .Select(g => new KnockoutRoundDto(
                Round: g.Key,
                Matches: g.OrderBy(m => m.Position).Select(m => new KnockoutMatchDto(
                    MatchId: m.Id,
                    Round: m.Round,
                    Position: m.Position,
                    BotOne: m.BotOne,
                    BotTwo: m.BotTwo,
                    GameId: m.GameId,
                    IsBye: m.IsBye,
                    IsCompleted: m.IsCompleted,
                    WinnerId: m.WinnerId)).ToList().AsReadOnly()))
            .ToList();

        return new TournamentBracketDto(
            tournament.Id,
            tournament.Name,
            tournament.Status.ToString(),
            tournament.WinnerId,
            participants,
            groupDtos.AsReadOnly(),
            knockoutRounds.AsReadOnly());
    }
}
