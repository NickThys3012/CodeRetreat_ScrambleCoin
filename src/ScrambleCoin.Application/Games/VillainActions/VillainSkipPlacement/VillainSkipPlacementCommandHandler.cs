using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Games.VillainActions.VillainPlacePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
namespace ScrambleCoin.Application.Games.VillainActions.VillainSkipPlacement;

/// <summary>
/// Handles <see cref="VillainSkipPlacementCommand"/>: skips placement for the villain.
/// </summary>
public sealed class VillainSkipPlacementCommandHandler : IRequestHandler<VillainSkipPlacementCommand, VillainPlacementResultDto>
{
    private readonly IGameRepository _gameRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<VillainSkipPlacementCommandHandler> _logger;

    public VillainSkipPlacementCommandHandler(
        IGameRepository gameRepository,
        IPublisher publisher,
        ILogger<VillainSkipPlacementCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<VillainPlacementResultDto> Handle(VillainSkipPlacementCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        if (game.VillainId is null)
            throw new DomainException("This game does not have a villain.");

        if (request.VillainPlayerId != game.PlayerTwo)
            throw new UnauthorizedGameAccessException();

        game.SkipPlacement(request.VillainPlayerId);

        // Capture events BEFORE SaveAsync clears them.
        var phaseEvents = game.DomainEvents.OfType<TurnPhaseAdvanced>().ToList();

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Villain skipped placement in game {GameId} on turn {Turn}",
            request.GameId, game.TurnNumber);

        foreach (var e in phaseEvents)
            await _publisher.Publish(
                new TurnPhaseChangedNotification(e.GameId, e.TurnNumber, e.PreviousPhase.ToString(), e.NewPhase?.ToString()),
                cancellationToken);

        return new VillainPlacementResultDto(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}
