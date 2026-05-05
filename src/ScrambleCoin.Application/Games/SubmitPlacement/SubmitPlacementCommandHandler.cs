using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.SubmitPlacement;

/// <summary>
/// Handles <see cref="SubmitPlacementCommand"/>: validates the action, delegates to the domain,
/// persists the game, and returns the resulting phase state.
/// </summary>
public sealed class SubmitPlacementCommandHandler : IRequestHandler<SubmitPlacementCommand, PlacementResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<SubmitPlacementCommandHandler> _logger;

    public SubmitPlacementCommandHandler(
        IGameRepository gameRepository,
        ILogger<SubmitPlacementCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task<PlacementResult> Handle(SubmitPlacementCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        switch (request.Action?.ToLowerInvariant())
        {
            case "place":
                if (request.PieceId is null || request.Position is null)
                    throw new DomainException("Action 'place' requires 'pieceId' and 'position'.");
                game.PlacePiece(
                    request.PlayerId,
                    request.PieceId.Value,
                    new Position(request.Position.Row, request.Position.Col));
                break;

            case "replace":
                if (request.PieceId is null || request.ReplacedPieceId is null)
                    throw new DomainException("Action 'replace' requires 'pieceId' and 'replacedPieceId'.");
                game.ReplacePiece(
                    request.PlayerId,
                    request.ReplacedPieceId.Value,
                    request.PieceId.Value);
                break;

            case "skip":
                game.SkipPlacement(request.PlayerId);
                break;

            default:
                throw new DomainException($"Unknown action '{request.Action}'. Valid values are: place, replace, skip.");
        }

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Placement action '{Action}' committed by player {PlayerId} in game {GameId} on turn {Turn}",
            request.Action, request.PlayerId, request.GameId, game.TurnNumber);

        return new PlacementResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}
