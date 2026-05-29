using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Behaviours;
using ScrambleCoin.Application.Games;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="SignalRBroadcastBehaviour{TRequest,TResponse}"/> (Issue #54).
/// </summary>
public class SignalRBroadcastBehaviourTests
{
    // ── Fake request types ────────────────────────────────────────────────────

    /// <summary>A command that implements <see cref="IGameStateChangingCommand"/>.</summary>
    private sealed record GameStateChangingRequest(Guid GameId)
        : IRequest<Unit>, IGameStateChangingCommand;

    /// <summary>A query/command that does NOT implement <see cref="IGameStateChangingCommand"/>.</summary>
    private sealed record NonGameStateChangingRequest() : IRequest<Unit>;

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static SignalRBroadcastBehaviour<TRequest, Unit> BuildBehaviour<TRequest>(
        IGameBroadcaster broadcaster)
        where TRequest : notnull
    {
        var logger = NullLogger<SignalRBroadcastBehaviour<TRequest, Unit>>.Instance;
        return new SignalRBroadcastBehaviour<TRequest, Unit>(broadcaster, logger);
    }

    private static RequestHandlerDelegate<Unit> SuccessDelegate()
        => _ => Task.FromResult(Unit.Value);

    private static RequestHandlerDelegate<Unit> ThrowingDelegate()
        => _ => Task.FromException<Unit>(new InvalidOperationException("handler failed"));

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithGameStateChangingCommand_CallsBroadcastBoardStateAsync()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var broadcaster = Substitute.For<IGameBroadcaster>();
        var behaviour = BuildBehaviour<GameStateChangingRequest>(broadcaster);
        var request = new GameStateChangingRequest(gameId);

        // Act
        await behaviour.Handle(request, SuccessDelegate(), CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastBoardStateAsync(gameId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithGameStateChangingCommand_PassesCorrectGameId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var broadcaster = Substitute.For<IGameBroadcaster>();
        var behaviour = BuildBehaviour<GameStateChangingRequest>(broadcaster);
        var request = new GameStateChangingRequest(gameId);

        var capturedId = Guid.Empty;
        await broadcaster
            .BroadcastBoardStateAsync(Arg.Do<Guid>(id => capturedId = id), Arg.Any<CancellationToken>());

        // Act
        await behaviour.Handle(request, SuccessDelegate(), CancellationToken.None);

        // Assert
        Assert.Equal(gameId, capturedId);
    }

    [Fact]
    public async Task Handle_WithNonGameStateChangingRequest_DoesNotCallBroadcast()
    {
        // Arrange
        var broadcaster = Substitute.For<IGameBroadcaster>();
        var behaviour = BuildBehaviour<NonGameStateChangingRequest>(broadcaster);
        var request = new NonGameStateChangingRequest();

        // Act
        await behaviour.Handle(request, SuccessDelegate(), CancellationToken.None);

        // Assert: broadcast must never be called for non-game-state-changing requests
        await broadcaster.DidNotReceive().BroadcastBoardStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_ExceptionPropagatesAndBroadcastIsNotCalled()
    {
        // Arrange
        var broadcaster = Substitute.For<IGameBroadcaster>();
        var behaviour = BuildBehaviour<GameStateChangingRequest>(broadcaster);
        var request = new GameStateChangingRequest(Guid.NewGuid());

        // Act & Assert: the handler exception must propagate
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behaviour.Handle(request, ThrowingDelegate(), CancellationToken.None));

        // And the broadcast must not be called after a failed command
        await broadcaster.DidNotReceive().BroadcastBoardStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBroadcastThrows_ExceptionIsSwallowedAndDoesNotPropagate()
    {
        // Arrange
        var broadcaster = Substitute.For<IGameBroadcaster>();
        broadcaster
            .BroadcastBoardStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SignalR is down"));

        var behaviour = BuildBehaviour<GameStateChangingRequest>(broadcaster);
        var request = new GameStateChangingRequest(Guid.NewGuid());

        // Act — must NOT throw even though the broadcaster explodes
        var exception = await Record.ExceptionAsync(
            () => behaviour.Handle(request, SuccessDelegate(), CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_WithGameStateChangingCommand_ReturnsHandlerResponse()
    {
        // Arrange — ensure the pipeline still returns the inner handler's result
        var broadcaster = Substitute.For<IGameBroadcaster>();
        var behaviour = BuildBehaviour<GameStateChangingRequest>(broadcaster);
        var request = new GameStateChangingRequest(Guid.NewGuid());

        // Act
        var result = await behaviour.Handle(request, SuccessDelegate(), CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result);
    }

    [Fact]
    public async Task Handle_WithNonGameStateChangingRequest_ReturnsHandlerResponse()
    {
        // Arrange
        var broadcaster = Substitute.For<IGameBroadcaster>();
        var behaviour = BuildBehaviour<NonGameStateChangingRequest>(broadcaster);
        var request = new NonGameStateChangingRequest();

        // Act
        var result = await behaviour.Handle(request, SuccessDelegate(), CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result);
    }
}
