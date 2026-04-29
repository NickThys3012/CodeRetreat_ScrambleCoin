using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Games.MovePiece;

/// <summary>
/// Handles <see cref="MovePieceCommand"/>: loads the game, delegates movement to the domain,
/// and persists the updated state.
/// </summary>
public sealed class MovePieceCommandHandler : IRequestHandler<MovePieceCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<MovePieceCommandHandler> _logger;

    public MovePieceCommandHandler(IGameRepository gameRepository, ILogger<MovePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task Handle(MovePieceCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        var turnNumber = game.TurnNumber;

        game.MovePiece(request.PlayerId, request.PieceId, request.Segments);

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Piece {PieceId} moved by player {PlayerId} in game {GameId} on turn {Turn}.",
            request.PieceId, request.PlayerId, request.GameId, turnNumber);
    }
}
