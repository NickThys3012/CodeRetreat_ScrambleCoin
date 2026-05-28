using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.Tournaments;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Tournament.StartTournament;

/// <summary>
/// Handles <see cref="StartTournamentCommand"/>:
/// <list type="bullet">
///   <item>Locks participants and generates the round-robin group schedule.</item>
///   <item>Creates a real game for each group match with both bot lineups set.</item>
///   <item>Creates BotRegistrations so bots can interact with their games using stable tokens.</item>
///   <item>Persists everything atomically.</item>
/// </list>
/// </summary>
public sealed class StartTournamentCommandHandler : IRequestHandler<StartTournamentCommand>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StartTournamentCommandHandler> _logger;

    public StartTournamentCommandHandler(
        ITournamentRepository tournamentRepository,
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        IUnitOfWork unitOfWork,
        ILogger<StartTournamentCommandHandler> logger)
    {
        _tournamentRepository = tournamentRepository;
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(StartTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);

        // Build a quick-lookup map: botId → participant
        var participantMap = tournament.Participants.ToDictionary(p => p.BotId);

        // Generate the round-robin schedule
        var groupMatches = tournament.Start();

        // Create a game for every group match
        foreach (var match in groupMatches)
        {
            var partOne = participantMap[match.BotOne];
            var partTwo = participantMap[match.BotTwo];

            (Guid gameId, Guid botOnePlayerId, Guid botOneToken, Guid botTwoPlayerId, Guid botTwoToken) =
                CreateGame(partOne, partTwo);

            match.AssignGame(gameId, botOnePlayerId, botOneToken, botTwoPlayerId, botTwoToken);

            // Stage game + registrations (commit once at the end)
            var game = BuildGame(gameId, botOnePlayerId, botTwoPlayerId, partOne.Lineup, partTwo.Lineup);
            await _gameRepository.StageAsync(game, cancellationToken);

            var regOne = new DomainBotReg(botOneToken, botOnePlayerId, gameId);
            var regTwo = new DomainBotReg(botTwoToken, botTwoPlayerId, gameId);
            await _botRegistrationRepository.StageAsync(regOne, cancellationToken);
            await _botRegistrationRepository.StageAsync(regTwo, cancellationToken);
        }

        // Persist tournament (with all match assignments) + all games + registrations atomically
        await _tournamentRepository.SaveAsync(tournament, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Tournament {TournamentId} started. {MatchCount} group matches scheduled.",
            request.TournamentId, groupMatches.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Generates stable IDs and tokens for a new tournament game.</summary>
    private static (Guid gameId, Guid botOnePlayerId, Guid botOneToken, Guid botTwoPlayerId, Guid botTwoToken)
        CreateGame(TournamentParticipant partOne, TournamentParticipant partTwo)
    {
        _ = partOne; // used by caller for lineup
        _ = partTwo;

        return (
            Guid.NewGuid(),
            Guid.NewGuid(), // player slot for botOne
            Guid.NewGuid(), // auth token for botOne
            Guid.NewGuid(), // player slot for botTwo
            Guid.NewGuid()  // auth token for botTwo
        );
    }

    /// <summary>
    /// Builds a started game aggregate with both lineups set.
    /// </summary>
    private static Game BuildGame(
        Guid gameId,
        Guid playerOneId,
        Guid playerTwoId,
        IReadOnlyList<string> lineupOne,
        IReadOnlyList<string> lineupTwo)
    {
        var board = new Board();

        // Use the internal Game constructor that accepts explicit IDs
        var game = new Game(gameId, playerOneId, playerTwoId, board);

        var lOne = BuildLineup(playerOneId, lineupOne);
        var lTwo = BuildLineup(playerTwoId, lineupTwo);

        game.SetLineup(playerOneId, lOne);
        game.SetLineup(playerTwoId, lTwo);
        game.Start();

        game.ClearDomainEvents();

        return game;
    }

    private static Lineup BuildLineup(Guid playerId, IReadOnlyList<string> pieceNames)
    {
        var pieces = pieceNames.Select(name => PieceFactory.Create(name, playerId)).ToList();
        return new Lineup(pieces);
    }
}
