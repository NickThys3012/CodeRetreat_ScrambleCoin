using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="VillainRegistry"/> (Issue #41).
/// Verifies the confirmed hardcoded lineups, slug tolerance, and the fallback behaviour for
/// unknown villain IDs.
/// </summary>
public class VillainRegistryTests
{
    private static List<string> PieceNames(Lineup lineup) =>
        lineup.Pieces.Select(p => p.Name).ToList();

    [Fact]
    public void GetLineupForVillain_Elsa_ReturnsConfirmedFivePieceLineup()
    {
        var playerId = Guid.NewGuid();

        var lineup = VillainRegistry.GetLineupForVillain(VillainRegistry.Elsa.Id, playerId);

        Assert.Equal(
            ["Mickey", "Donald", "WALL•E", "Merlin", "Scrooge"],
            PieceNames(lineup));
        Assert.All(lineup.Pieces, p => Assert.Equal(playerId, p.PlayerId));
    }

    [Fact]
    public void GetLineupForVillain_Ursula_ReturnsConfirmedFivePieceLineup()
    {
        var playerId = Guid.NewGuid();

        var lineup = VillainRegistry.GetLineupForVillain(VillainRegistry.Ursula.Id, playerId);

        Assert.Equal(
            ["Mickey", "Donald", "Anna", "Goofy", "Cinderella"],
            PieceNames(lineup));
    }

    [Fact]
    public void GetLineupForVillain_Gaston_ReturnsConfirmedFivePieceLineup()
    {
        var playerId = Guid.NewGuid();

        var lineup = VillainRegistry.GetLineupForVillain(VillainRegistry.Gaston.Id, playerId);

        Assert.Equal(
            ["Minnie", "Goofy", "Donald", "Scrooge", "Mickey"],
            PieceNames(lineup));
    }

    [Theory]
    [InlineData("Elsa")]
    [InlineData("ELSA")]
    [InlineData("  elsa  ")]
    public void GetLineupForVillain_IsTolerantOfSlugFormat(string villainId)
    {
        var playerId = Guid.NewGuid();

        var lineup = VillainRegistry.GetLineupForVillain(villainId, playerId);

        Assert.Equal(
            ["Mickey", "Donald", "WALL•E", "Merlin", "Scrooge"],
            PieceNames(lineup));
    }

    [Fact]
    public void GetLineupForVillain_UnknownId_Throws()
    {
        var playerId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            VillainRegistry.GetLineupForVillain("not-a-villain", playerId));
    }

    [Fact]
    public void GetLineupForVillainOrDefault_UnknownId_ReturnsDefaultLineup()
    {
        var playerId = Guid.NewGuid();

        var lineup = VillainRegistry.GetLineupForVillainOrDefault("scar", playerId);

        Assert.Equal(PieceNames(VillainRegistry.GetDefaultLineup(playerId)), PieceNames(lineup));
        Assert.Equal(Lineup.RequiredPieceCount, lineup.Pieces.Count);
    }

    [Fact]
    public void GetLineupForVillainOrDefault_KnownId_ReturnsBespokeLineup()
    {
        var playerId = Guid.NewGuid();

        var lineup = VillainRegistry.GetLineupForVillainOrDefault(VillainRegistry.Gaston.Id, playerId);

        Assert.Equal(
            ["Minnie", "Goofy", "Donald", "Scrooge", "Mickey"],
            PieceNames(lineup));
    }

    [Theory]
    [InlineData("elsa", true)]
    [InlineData("Ursula", true)]
    [InlineData("GASTON", true)]
    [InlineData("scar", false)]
    [InlineData("", false)]
    public void IsKnown_ReflectsRegisteredVillains(string villainId, bool expected)
    {
        Assert.Equal(expected, VillainRegistry.IsKnown(villainId));
    }

    [Theory]
    [InlineData("elsa", "Elsa")]
    [InlineData("Ursula", "Ursula")]
    [InlineData("GASTON", "Gaston")]
    public void GetDisplayName_ReturnsExpectedName(string villainId, string expected)
    {
        Assert.Equal(expected, VillainRegistry.GetDisplayName(villainId));
    }
}
