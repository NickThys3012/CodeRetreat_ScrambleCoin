using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Games.CreateGame;

/// <summary>
/// Handles <see cref="CreateGameCommand"/>: creates a game shell, persists it, and returns the <c>gameId</c>.
/// </summary>
public sealed class CreateGameCommandHandler : IRequestHandler<CreateGameCommand, CreateGameResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<CreateGameCommandHandler> _logger;

    public CreateGameCommandHandler(IGameRepository gameRepository, ILogger<CreateGameCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task<CreateGameResult> Handle(CreateGameCommand request, CancellationToken cancellationToken)
    {
        var board = new Board();
        var game = Game.CreateShell(board);

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation("Game shell created: {GameId} (PlayerOneSlot={PlayerOne}, PlayerTwoSlot={PlayerTwo})",
            game.Id, game.PlayerOne, game.PlayerTwo);

        return new CreateGameResult(game.Id);
    }
}
