using MediatR;
using ScrambleCoin.Application.Games.VillainActions.VillainMovePiece;
using ScrambleCoin.Application.Games.VillainActions.VillainPlacePiece;
using ScrambleCoin.Application.Games.VillainActions.VillainSkipMovement;
using ScrambleCoin.Application.Games.VillainActions.VillainSkipPlacement;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Translates a decided <see cref="VillainAction"/> into the corresponding villain MediatR command
/// and dispatches it. Villain actions go through the same command pipeline as bot actions — there
/// is no special domain path.
/// </summary>
public interface IVillainActionDispatcher
{
    /// <summary>
    /// Executes a villain action by dispatching the appropriate MediatR command.
    /// </summary>
    Task ExecuteVillainActionAsync(
        VillainAction action,
        Guid gameId,
        Guid villainPlayerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IVillainActionDispatcher"/>.
/// </summary>
public sealed class VillainActionDispatcher : IVillainActionDispatcher
{
    private readonly IMediator _mediator;

    public VillainActionDispatcher(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task ExecuteVillainActionAsync(
        VillainAction action,
        Guid gameId,
        Guid villainPlayerId,
        CancellationToken cancellationToken = default)
    {
        switch (action)
        {
            case PlacementAction placement:
                await _mediator.Send(
                    new VillainPlacePieceCommand(gameId, villainPlayerId, placement.PieceId, placement.Position),
                    cancellationToken);
                break;

            case SkipPlacementAction:
                await _mediator.Send(
                    new VillainSkipPlacementCommand(gameId, villainPlayerId),
                    cancellationToken);
                break;

            case MovementAction movement:
                await _mediator.Send(
                    new VillainMovePieceCommand(gameId, villainPlayerId, movement.PieceId, movement.Segments),
                    cancellationToken);
                break;

            case SkipMovementAction:
                await _mediator.Send(
                    new VillainSkipMovementCommand(gameId, villainPlayerId),
                    cancellationToken);
                break;

            default:
                throw new DomainException($"Unknown villain action type: {action.GetType().Name}");
        }
    }
}
