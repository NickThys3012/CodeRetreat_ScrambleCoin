using MediatR;
using ScrambleCoin.Application.Games.VillainActions.VillainMovePiece;
using ScrambleCoin.Application.Games.VillainActions.VillainPlacePiece;
using ScrambleCoin.Application.Games.VillainActions.VillainSkipMovement;
using ScrambleCoin.Application.Games.VillainActions.VillainSkipPlacement;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Dispatcher that converts villain actions into MediatR commands and executes them.
/// </summary>
public interface IVillainActionDispatcher
{
    /// <summary>
    /// Executes a villain action by dispatching the appropriate MediatR command.
    /// </summary>
    /// <param name="action">The villain action to execute.</param>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="villainPlayerId">The player ID of the villain (typically PlayerTwo).</param>
    /// <param name="mediator">The MediatR mediator instance for dispatching commands.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task ExecuteVillainActionAsync(
        VillainAction action,
        Guid gameId,
        Guid villainPlayerId,
        IMediator mediator,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IVillainActionDispatcher"/>.
/// Converts VillainAction objects into villain-specific MediatR commands.
/// </summary>
public sealed class VillainActionDispatcher : IVillainActionDispatcher
{
    public async Task ExecuteVillainActionAsync(
        VillainAction action,
        Guid gameId,
        Guid villainPlayerId,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        switch (action)
        {
            case PlacementAction placement:
                await mediator.Send(
                    new VillainPlacePieceCommand(gameId, villainPlayerId, placement.PieceId, placement.Position),
                    cancellationToken);
                break;

            case SkipPlacementAction:
                await mediator.Send(
                    new VillainSkipPlacementCommand(gameId, villainPlayerId),
                    cancellationToken);
                break;

            case MovementAction movement:
                await mediator.Send(
                    new VillainMovePieceCommand(gameId, villainPlayerId, movement.PieceId, movement.Segments),
                    cancellationToken);
                break;

            case SkipMovementAction:
                await mediator.Send(
                    new VillainSkipMovementCommand(gameId, villainPlayerId),
                    cancellationToken);
                break;

            default:
                throw new DomainException($"Unknown villain action type: {action.GetType().Name}");
        }
    }
}
