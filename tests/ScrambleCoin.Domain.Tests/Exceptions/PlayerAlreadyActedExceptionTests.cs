using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.Tests.Exceptions;

/// <summary>
/// Unit tests for <see cref="PlayerAlreadyActedException"/> (Issue #39).
/// </summary>
public class PlayerAlreadyActedExceptionTests
{
    // ── Type hierarchy ────────────────────────────────────────────────────────

    [Fact]
    public void PlayerAlreadyActedException_IsSubtypeOfDomainException()
    {
        var playerId = Guid.NewGuid();

        var exception = new PlayerAlreadyActedException(playerId);

        Assert.IsAssignableFrom<DomainException>(exception);
    }

    // ── Message ───────────────────────────────────────────────────────────────

    [Fact]
    public void PlayerAlreadyActedException_Message_ContainsPlayerId()
    {
        var playerId = Guid.NewGuid();

        var exception = new PlayerAlreadyActedException(playerId);

        Assert.Contains(playerId.ToString(), exception.Message);
    }

    [Fact]
    public void PlayerAlreadyActedException_Message_IsNotNullOrEmpty()
    {
        var playerId = Guid.NewGuid();

        var exception = new PlayerAlreadyActedException(playerId);

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }
}
