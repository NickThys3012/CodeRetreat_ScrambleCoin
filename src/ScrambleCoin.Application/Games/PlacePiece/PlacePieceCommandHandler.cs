using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Games.PlacePiece;

/// <summary>
/// Handles <see cref="PlacePieceCommand"/>: loads the game, delegates placement to the domain,
/// and persists the updated game.
/// </summary>
public sealed class PlacePieceCommandHandler : IRequestHandler<PlacePieceCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<PlacePieceCommandHandler> _logger;

    /// <param name="gameRepository">Repository for loading and saving games.</param>
    /// <param name="logger">Logger for structured output.</param>
    public PlacePieceCommandHandler(IGameRepository gameRepository, ILogger<PlacePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(PlacePieceCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        game.PlacePiece(request.PlayerId, request.PieceId, request.TargetPosition);

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Piece {PieceId} placed at {Position} by player {PlayerId} in game {GameId} on turn {Turn}",
            request.PieceId, request.TargetPosition, request.PlayerId, request.GameId, game.TurnNumber);
    }
}
