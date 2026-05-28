using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Games.Matchmaking;

/// <summary>
/// Handles <see cref="StartMatchCommand"/>: creates a game shell, assigns both bot lineups,
/// starts the game, persists the game and both bot registrations in one atomic operation.
/// </summary>
public sealed class StartMatchCommandHandler : IRequestHandler<StartMatchCommand, StartMatchResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StartMatchCommandHandler> _logger;

    public StartMatchCommandHandler(
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        IUnitOfWork unitOfWork,
        ILogger<StartMatchCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StartMatchResult> Handle(StartMatchCommand request, CancellationToken cancellationToken)
    {
        var board = new Board();
        var game = Game.CreateShell(board);

        // Assign waiting bot → PlayerOne slot.
        var tokenOne = Guid.NewGuid();
        var lineupOne = BuildLineup(game.PlayerOne, request.LineupOne);
        game.SetLineup(game.PlayerOne, lineupOne);

        // Assign incoming bot → PlayerTwo slot.
        var tokenTwo = Guid.NewGuid();
        var lineupTwo = BuildLineup(game.PlayerTwo, request.LineupTwo);
        game.SetLineup(game.PlayerTwo, lineupTwo);

        game.Start();

        var regOne = new DomainBotReg(tokenOne, game.PlayerOne, game.Id);
        var regTwo = new DomainBotReg(tokenTwo, game.PlayerTwo, game.Id);

        // Stage all changes on the shared DbContext, then commit once (Fix 4).
        await _gameRepository.StageAsync(game, cancellationToken);
        await _botRegistrationRepository.StageAsync(regOne, cancellationToken);
        await _botRegistrationRepository.StageAsync(regTwo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Clear domain events after successful persistence (they are transient).
        game.ClearDomainEvents();

        _logger.LogInformation(
            "Match started via StartMatchCommand: GameId={GameId}, P1={PlayerOne}, P2={PlayerTwo}",
            game.Id, game.PlayerOne, game.PlayerTwo);

        return new StartMatchResult(
            game.Id,
            game.PlayerOne,
            tokenOne,
            game.PlayerTwo,
            tokenTwo);
    }

    private static Lineup BuildLineup(Guid playerId, IReadOnlyList<string> pieceNames)
    {
        var pieces = pieceNames.Select(name => PieceFactory.Create(name, playerId)).ToList();
        return new Lineup(pieces);
    }
}
