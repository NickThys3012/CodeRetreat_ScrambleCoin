using MediatR;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Games.Admin;

/// <summary>
/// Handles <see cref="GetAllActiveGamesQuery"/>: delegates to
/// <see cref="IGameRepository.GetAllActiveAsync"/> to return lightweight game summaries
/// without reconstructing full Game aggregates.
/// </summary>
public sealed class GetAllActiveGamesQueryHandler
    : IRequestHandler<GetAllActiveGamesQuery, IReadOnlyList<ActiveGameSummaryDto>>
{
    private readonly IGameRepository _gameRepository;

    public GetAllActiveGamesQueryHandler(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    public Task<IReadOnlyList<ActiveGameSummaryDto>> Handle(
        GetAllActiveGamesQuery request,
        CancellationToken cancellationToken)
    {
        return _gameRepository.GetAllActiveAsync(cancellationToken);
    }
}
