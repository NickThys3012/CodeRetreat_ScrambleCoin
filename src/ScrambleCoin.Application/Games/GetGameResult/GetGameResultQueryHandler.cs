using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Games.GetGameResult;

/// <summary>
/// Handles <see cref="GetGameResultQuery"/>:
/// <list type="bullet">
///   <item>Authenticates the bot token via <see cref="IBotRegistrationRepository"/>.</item>
///   <item>Loads the game via <see cref="IGameRepository"/>.</item>
///   <item>Throws <see cref="GameNotFinishedException"/> when the game is still in progress.</item>
///   <item>Returns <see cref="GetGameResultDto"/> with final scores and winner information.</item>
/// </list>
/// </summary>
public sealed class GetGameResultQueryHandler : IRequestHandler<GetGameResultQuery, GetGameResultDto>
{
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly ILogger<GetGameResultQueryHandler> _logger;

    public GetGameResultQueryHandler(
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        ILogger<GetGameResultQueryHandler> logger)
    {
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _logger = logger;
    }

    public async Task<GetGameResultDto> Handle(GetGameResultQuery request, CancellationToken cancellationToken)
    {
        // 1. Validate bot token — must belong to this game.
        var registration = await _botRegistrationRepository.GetByTokenAsync(request.BotToken, cancellationToken);

        if (registration is null || registration.GameId != request.GameId)
            throw new UnauthorizedGameAccessException();

        // 2. Load game (throws GameNotFoundException if missing).
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        // 3. Game must be finished.
        if (game.Status != GameStatus.Finished)
            throw new GameNotFinishedException(game.Id);

        // 4. Calculate winner.
        game.Scores.TryGetValue(game.PlayerOne, out var scoreOne);
        game.Scores.TryGetValue(game.PlayerTwo, out var scoreTwo);

        var isDraw = scoreOne == scoreTwo;
        Guid? winnerId = isDraw ? null : (scoreOne > scoreTwo ? game.PlayerOne : game.PlayerTwo);

        _logger.LogInformation(
            "Game result queried: GameId={GameId} P1Score={P1Score} P2Score={P2Score} Winner={Winner}",
            game.Id, scoreOne, scoreTwo, winnerId?.ToString() ?? "draw");

        return new GetGameResultDto(
            GameId: game.Id,
            Status: "finished",
            PlayerOneId: game.PlayerOne,
            PlayerOneScore: scoreOne,
            PlayerTwoId: game.PlayerTwo,
            PlayerTwoScore: scoreTwo,
            WinnerId: winnerId,
            IsDraw: isDraw);
    }
}
