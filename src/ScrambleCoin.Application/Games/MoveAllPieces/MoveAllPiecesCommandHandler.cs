using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Games.MoveAllPieces;

/// <summary>
/// Handles <see cref="MoveAllPiecesCommand"/>: loads the game, delegates movement to the domain,
/// and persists the updated state.
/// </summary>
public sealed class MoveAllPiecesCommandHandler : IRequestHandler<MoveAllPiecesCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<MoveAllPiecesCommandHandler> _logger;

    /// <param name="gameRepository">Repository for loading and saving games.</param>
    /// <param name="logger">Logger for structured output.</param>
    public MoveAllPiecesCommandHandler(
        IGameRepository gameRepository,
        ILogger<MoveAllPiecesCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task Handle(MoveAllPiecesCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        var domainMoves = request.Moves
            .Select(m => (m.PieceId, m.Segments))
            .ToList();

        var turnNumber = game.TurnNumber;

        game.MoveAllPieces(request.PlayerId, domainMoves);

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Moves submitted for game {GameId} by player {PlayerId} on turn {Turn} ({PieceCount} piece(s)).",
            request.GameId, request.PlayerId, turnNumber, request.Moves.Count);
    }
}
