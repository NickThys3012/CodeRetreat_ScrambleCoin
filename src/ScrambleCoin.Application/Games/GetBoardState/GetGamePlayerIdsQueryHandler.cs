using MediatR;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Games.GetBoardState;

/// <summary>
/// Handles <see cref="GetGamePlayerIdsQuery"/>: loads the game and returns its two
/// participant IDs, current phase, and active mover — without building the full board DTO.
/// </summary>
public sealed class GetGamePlayerIdsQueryHandler : IRequestHandler<GetGamePlayerIdsQuery, GamePlayerIdsDto>
{
    private readonly IGameRepository _gameRepository;

    public GetGamePlayerIdsQueryHandler(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    public async Task<GamePlayerIdsDto> Handle(GetGamePlayerIdsQuery request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        return new GamePlayerIdsDto(
            PlayerOne:   game.PlayerOne,
            PlayerTwo:   game.PlayerTwo,
            Phase:       game.CurrentPhase?.ToString(),
            ActiveMover: game.MovePhaseActivePlayer);
    }
}
