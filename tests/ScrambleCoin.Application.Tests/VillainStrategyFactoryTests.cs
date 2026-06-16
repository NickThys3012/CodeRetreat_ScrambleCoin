using ScrambleCoin.Application.Services;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Application.Services.Villains.Implementations;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="VillainStrategyFactory"/> (Issue #41).
/// Verifies the correct strategy type, lineup, and display name are resolved per villain ID.
/// </summary>
public class VillainStrategyFactoryTests
{
    private readonly VillainStrategyFactory _factory = new();

    [Fact]
    public void CreateStrategy_Elsa_ReturnsElsaStrategy()
    {
        var strategy = _factory.CreateStrategy("elsa");

        Assert.IsType<ElsaStrategy>(strategy);
        Assert.IsAssignableFrom<IVillainStrategy>(strategy);
    }

    [Fact]
    public void CreateStrategy_Ursula_ReturnsUrsulaStrategy()
    {
        Assert.IsType<UrsulaStrategy>(_factory.CreateStrategy("Ursula"));
    }

    [Fact]
    public void CreateStrategy_Gaston_ReturnsGastonStrategy()
    {
        Assert.IsType<GastonStrategy>(_factory.CreateStrategy("GASTON"));
    }

    [Fact]
    public void CreateStrategy_UnknownVillain_Throws()
    {
        Assert.Throws<DomainException>(() => _factory.CreateStrategy("scar"));
    }

    [Theory]
    [InlineData("elsa", "Elsa")]
    [InlineData("ursula", "Ursula")]
    [InlineData("gaston", "Gaston")]
    public void GetDisplayName_ReturnsExpectedName(string villainId, string expected)
    {
        Assert.Equal(expected, _factory.GetDisplayName(villainId));
    }

    [Fact]
    public void GetVillainLineup_Elsa_ReturnsBespokeLineupOwnedByPlayer()
    {
        var playerId = Guid.NewGuid();

        var lineup = _factory.GetVillainLineup("elsa", playerId);

        Assert.Equal(
            new[] { "Mickey", "Donald", "WALL•E", "Merlin", "Scrooge" },
            lineup.Pieces.Select(p => p.Name).ToList());
        Assert.All(lineup.Pieces, p => Assert.Equal(playerId, p.PlayerId));
    }
}
