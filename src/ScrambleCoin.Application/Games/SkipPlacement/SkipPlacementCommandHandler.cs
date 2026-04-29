using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Games.SkipPlacement;

/// <summary>
/// Handles <see cref="SkipPlacementCommand"/>: loads the game, records the player's skip,
/// and persists the updated game.
/// </summary>
public sealed class SkipPlacementCommandHandler : IRequestHandler<SkipPlacementCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<SkipPlacementCommandHandler> _logger;

    /// <param name="gameRepository">Repository for loading and saving games.</param>
    /// <param name="logger">Logger for structured output.</param>
    public SkipPlacementCommandHandler(IGameRepository gameRepository, ILogger<SkipPlacementCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(SkipPlacementCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        game.SkipPlacement(request.PlayerId);

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Player {PlayerId} skipped placement in game {GameId} on turn {Turn}",
            request.PlayerId, request.GameId, game.TurnNumber);
    }
}
