using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Games.ReplacePiece;

/// <summary>
/// Handles <see cref="ReplacePieceCommand"/>: loads the game, delegates the replace operation
/// to the domain, and persists the updated game.
/// </summary>
public sealed class ReplacePieceCommandHandler : IRequestHandler<ReplacePieceCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<ReplacePieceCommandHandler> _logger;

    /// <param name="gameRepository">Repository for loading and saving games.</param>
    /// <param name="logger">Logger for structured output.</param>
    public ReplacePieceCommandHandler(IGameRepository gameRepository, ILogger<ReplacePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(ReplacePieceCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        game.ReplacePiece(request.PlayerId, request.ExistingPieceId, request.NewPieceId, request.TargetPosition);

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Piece {ExistingPieceId} replaced by {NewPieceId} at {Position} by player {PlayerId} in game {GameId} on turn {Turn}",
            request.ExistingPieceId, request.NewPieceId, request.TargetPosition, request.PlayerId, request.GameId, game.TurnNumber);
    }
}
