using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.JoinGame;

/// <summary>
/// Handles <see cref="JoinGameCommand"/>:
/// <list type="bullet">
///   <item>Loads the game.</item>
///   <item>Determines the next available player slot (<c>PlayerOne</c> or <c>PlayerTwo</c>).</item>
///   <item>Builds the bot's lineup from piece names using <see cref="PieceFactory"/>.</item>
///   <item>Sets the lineup on the game via <c>game.SetLineup</c>.</item>
///   <item>Creates and persists a <see cref="BotRegistration"/> containing the bearer token.</item>
///   <item>If both slots are now filled, calls <c>game.Start()</c> and publishes <see cref="TurnRolledOver"/> to trigger coin spawning.</item>
/// </list>
/// </summary>
public sealed class JoinGameCommandHandler : IRequestHandler<JoinGameCommand, JoinGameResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<JoinGameCommandHandler> _logger;

    public JoinGameCommandHandler(
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        IPublisher publisher,
        ILogger<JoinGameCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<JoinGameResult> Handle(JoinGameCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        // Determine which slot is open.
        Guid assignedPlayerId;
        if (game.LineupPlayerOne is null)
            assignedPlayerId = game.PlayerOne;
        else if (game.LineupPlayerTwo is null)
            assignedPlayerId = game.PlayerTwo;
        else
            throw new GameFullException(request.GameId);

        // Build the lineup from piece names.
        var pieces = request.LineupPieceNames
            .Select(name => PieceFactory.Create(name, assignedPlayerId))
            .ToList();

        var lineup = new Lineup(pieces);
        game.SetLineup(assignedPlayerId, lineup);

        var gameJustStarted = false;
        if (game.LineupPlayerOne is not null && game.LineupPlayerTwo is not null)
        {
            game.Start();
            gameJustStarted = true;
            _logger.LogInformation("Game {GameId} started — both players joined. BotId={BotId} Turn={Turn}", game.Id, assignedPlayerId, game.TurnNumber);
        }

        // Issue a bearer token for this bot.
        var token = Guid.NewGuid();
        var registration = new DomainBotReg(token, assignedPlayerId, game.Id);

        await _gameRepository.SaveAsync(game, cancellationToken);
        await _botRegistrationRepository.SaveAsync(registration, cancellationToken);

        _logger.LogInformation(
            "Bot joined game {GameId}: BotId={BotId}, Slot={Slot}, Turn={Turn}",
            game.Id, assignedPlayerId,
            assignedPlayerId == game.PlayerOne ? "PlayerOne" : "PlayerTwo",
            game.TurnNumber);

        // Trigger coin spawn for turn 1 AFTER saving so the handler has a consistent DB state.
        if (gameJustStarted)
            await _publisher.Publish(new TurnRolledOver(game.Id), cancellationToken);

        return new JoinGameResult(assignedPlayerId, token);
    }
}
