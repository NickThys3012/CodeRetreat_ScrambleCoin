using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using ScrambleCoin.Web.Hubs;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Unit tests for <see cref="GameHub"/> (Issue #54).
/// Verifies that JoinGame and LeaveGame correctly manage SignalR group membership
/// using the connection-scoped <c>IGroupManager</c>.
/// </summary>
public class GameHubTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="GameHub"/> wired up with mock SignalR infrastructure.
    /// </summary>
    private static (GameHub hub, IGroupManager groups) BuildHub(string connectionId = "conn-abc-123")
    {
        var groups = Substitute.For<IGroupManager>();

        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);

        var hub = new GameHub();
        hub.Context = context;
        hub.Groups = groups;

        return (hub, groups);
    }

    // ── JoinGame ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinGame_AddsConnectionToCorrectGroup()
    {
        // Arrange
        var (hub, groups) = BuildHub("conn-spectator-1");
        var gameId = Guid.NewGuid().ToString();

        // Act
        await hub.JoinGame(gameId);

        // Assert
        await groups.Received(1).AddToGroupAsync(
            "conn-spectator-1",
            $"game-{gameId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGame_PrefixesGroupNameWithGame()
    {
        // Arrange
        var (hub, groups) = BuildHub();
        const string rawId = "some-guid-value";

        // Act
        await hub.JoinGame(rawId);

        // Assert — group name must be "game-{gameId}" exactly
        await groups.Received(1).AddToGroupAsync(
            Arg.Any<string>(),
            "game-some-guid-value",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGame_DoesNotRemoveFromGroup()
    {
        // Arrange
        var (hub, groups) = BuildHub();

        // Act
        await hub.JoinGame(Guid.NewGuid().ToString());

        // Assert — JoinGame must never call RemoveFromGroupAsync
        await groups.DidNotReceive().RemoveFromGroupAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ── LeaveGame ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveGame_RemovesConnectionFromCorrectGroup()
    {
        // Arrange
        var (hub, groups) = BuildHub("conn-spectator-2");
        var gameId = Guid.NewGuid().ToString();

        // Act
        await hub.LeaveGame(gameId);

        // Assert
        await groups.Received(1).RemoveFromGroupAsync(
            "conn-spectator-2",
            $"game-{gameId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGame_PrefixesGroupNameWithGame()
    {
        // Arrange
        var (hub, groups) = BuildHub();
        const string rawId = "another-guid";

        // Act
        await hub.LeaveGame(rawId);

        // Assert
        await groups.Received(1).RemoveFromGroupAsync(
            Arg.Any<string>(),
            "game-another-guid",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGame_DoesNotAddToGroup()
    {
        // Arrange
        var (hub, groups) = BuildHub();

        // Act
        await hub.LeaveGame(Guid.NewGuid().ToString());

        // Assert — LeaveGame must never call AddToGroupAsync
        await groups.DidNotReceive().AddToGroupAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ── Connection ID forwarding ──────────────────────────────────────────────

    [Fact]
    public async Task JoinGame_UsesContextConnectionId()
    {
        // Arrange — verify the hub passes the CALLER's ConnectionId, not a hard-coded value
        const string specificConnectionId = "unique-connection-xyz";
        var (hub, groups) = BuildHub(specificConnectionId);

        // Act
        await hub.JoinGame("any-game-id");

        // Assert
        await groups.Received(1).AddToGroupAsync(
            specificConnectionId,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGame_UsesContextConnectionId()
    {
        // Arrange
        const string specificConnectionId = "unique-connection-abc";
        var (hub, groups) = BuildHub(specificConnectionId);

        // Act
        await hub.LeaveGame("any-game-id");

        // Assert
        await groups.Received(1).RemoveFromGroupAsync(
            specificConnectionId,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
