using MediatR;
using NSubstitute;
using ScrambleCoin.Application.Games.VillainActions.VillainMovePiece;
using ScrambleCoin.Application.Games.VillainActions.VillainPlacePiece;
using ScrambleCoin.Application.Games.VillainActions.VillainSkipMovement;
using ScrambleCoin.Application.Games.VillainActions.VillainSkipPlacement;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="VillainActionDispatcher"/> (Issue #41).
/// Verifies each <see cref="VillainAction"/> variant is mapped to the correct villain MediatR
/// command with the correct parameters — villain actions use the same command pipeline as bots.
/// </summary>
public class VillainActionDispatcherTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly VillainActionDispatcher _dispatcher;

    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _villainPlayerId = Guid.NewGuid();

    public VillainActionDispatcherTests()
    {
        _dispatcher = new VillainActionDispatcher(_mediator);
    }

    [Fact]
    public async Task ExecuteVillainActionAsync_Placement_SendsVillainPlacePieceCommand()
    {
        var pieceId = Guid.NewGuid();
        var position = new Position(2, 3);

        await _dispatcher.ExecuteVillainActionAsync(
            new PlacementAction(pieceId, position), _gameId, _villainPlayerId);

        await _mediator.Received(1).Send(
            Arg.Is<VillainPlacePieceCommand>(c =>
                c.GameId == _gameId &&
                c.VillainPlayerId == _villainPlayerId &&
                c.PieceId == pieceId &&
                c.Position == position),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteVillainActionAsync_SkipPlacement_SendsVillainSkipPlacementCommand()
    {
        await _dispatcher.ExecuteVillainActionAsync(
            new SkipPlacementAction(), _gameId, _villainPlayerId);

        await _mediator.Received(1).Send(
            Arg.Is<VillainSkipPlacementCommand>(c =>
                c.GameId == _gameId &&
                c.VillainPlayerId == _villainPlayerId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteVillainActionAsync_Movement_SendsVillainMovePieceCommandWithSegments()
    {
        var pieceId = Guid.NewGuid();
        var segments = new List<IReadOnlyList<Position>>
        {
            new List<Position> { new(1, 1) }
        };

        await _dispatcher.ExecuteVillainActionAsync(
            new MovementAction(pieceId, segments), _gameId, _villainPlayerId);

        await _mediator.Received(1).Send(
            Arg.Is<VillainMovePieceCommand>(c =>
                c.GameId == _gameId &&
                c.VillainPlayerId == _villainPlayerId &&
                c.PieceId == pieceId &&
                ReferenceEquals(c.Segments, segments)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteVillainActionAsync_SkipMovement_SendsVillainSkipMovementCommand()
    {
        await _dispatcher.ExecuteVillainActionAsync(
            new SkipMovementAction(), _gameId, _villainPlayerId);

        await _mediator.Received(1).Send(
            Arg.Is<VillainSkipMovementCommand>(c =>
                c.GameId == _gameId &&
                c.VillainPlayerId == _villainPlayerId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteVillainActionAsync_UnknownActionType_Throws()
    {
        var unknown = new UnknownVillainAction();

        await Assert.ThrowsAsync<DomainException>(() =>
            _dispatcher.ExecuteVillainActionAsync(unknown, _gameId, _villainPlayerId));
    }

    private sealed record UnknownVillainAction : VillainAction;
}
