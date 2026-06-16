using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Games.SoloMode.RecordVillainDefeated;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Application.Notifications;

/// <summary>
/// Reacts to a <see cref="GameFinished"/> notification by recording the villain defeat
/// for solo mode games where the bot (PlayerOne) won.
/// </summary>
public sealed class GameFinishedNotificationHandler : INotificationHandler<GameFinished>
{
    private readonly IGameRepository _gameRepository;
    private readonly IVillainTreeRepository _villainTreeRepository;
    private readonly ISender _sender;
    private readonly ILogger<GameFinishedNotificationHandler> _logger;

    public GameFinishedNotificationHandler(
        IGameRepository gameRepository,
        IVillainTreeRepository villainTreeRepository,
        ISender sender,
        ILogger<GameFinishedNotificationHandler> logger)
    {
        _gameRepository = gameRepository;
        _villainTreeRepository = villainTreeRepository;
        _sender = sender;
        _logger = logger;
    }

    public async Task Handle(GameFinished notification, CancellationToken cancellationToken)
    {
        // Only proceed if bot won (not a draw and bot is the winner)
        if (notification.WinnerId is null)
        {
            _logger.LogInformation("Game {GameId} ended in a draw. No villain defeat recorded.", notification.GameId);
            return;
        }

        try
        {
            var game = await _gameRepository.GetByIdAsync(notification.GameId, cancellationToken);

            // Only record for solo mode games
            if (game.GameMode != GameMode.Solo || string.IsNullOrEmpty(game.VillainId))
            {
                _logger.LogDebug(
                    "Game {GameId} is not a solo mode game. No villain defeat recorded.",
                    notification.GameId);
                return;
            }

            // Only record if PlayerOne (the bot) won
            if (notification.WinnerId != game.PlayerOne)
            {
                _logger.LogInformation(
                    "Bot {BotId} lost solo game {GameId} vs villain {VillainId}.",
                    game.PlayerOne, notification.GameId, game.VillainId);
                return;
            }

            // Get the villain to find the unlocked piece
            var villain = await _villainTreeRepository.GetNodeByVillainIdAsync(game.VillainId, cancellationToken);
            if (villain is null)
            {
                _logger.LogWarning(
                    "Villain {VillainId} not found when recording defeat for game {GameId}.",
                    game.VillainId, notification.GameId);
                return;
            }

            // Record the defeat
            await _sender.Send(
                new RecordVillainDefeatedCommand(game.PlayerOne, game.VillainId, villain.UnlockedPieceId),
                cancellationToken);

            _logger.LogInformation(
                "Bot {BotId} defeated villain {VillainId} in solo game {GameId}. Unlocked piece: {PieceId}",
                game.PlayerOne, game.VillainId, notification.GameId, villain.UnlockedPieceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording villain defeat for game {GameId}", notification.GameId);
            // Don't re-throw; let the notification handler complete gracefully
        }
    }
}
