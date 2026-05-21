using ScrambleCoin.Application.Services.Villains.Implementations;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Services.Villains;

/// <summary>
/// Factory for creating villain AI strategy instances.
/// </summary>
public interface IVillainStrategyFactory
{
    /// <summary>
    /// Creates a villain strategy by villain ID.
    /// </summary>
    IVillainStrategy CreateStrategy(string villainId);

    /// <summary>
    /// Gets the display name for a villain.
    /// </summary>
    string GetDisplayName(string villainId);

    /// <summary>
    /// Gets the villain's hardcoded lineup.
    /// </summary>
    Domain.ValueObjects.Lineup GetVillainLineup(string villainId, Guid playerId);
}

/// <summary>
/// Default implementation of the villain strategy factory.
/// </summary>
public sealed class VillainStrategyFactory : IVillainStrategyFactory
{
    public IVillainStrategy CreateStrategy(string villainId) =>
        villainId.ToLowerInvariant() switch
        {
            VillainRegistry.Elsa.Id => new ElsaStrategy(),
            VillainRegistry.Ursula.Id => new UrsulaStrategy(),
            VillainRegistry.Gaston.Id => new GastonStrategy(),
            _ => throw new DomainException($"Unknown villain ID: {villainId}")
        };

    public string GetDisplayName(string villainId) =>
        VillainRegistry.GetDisplayName(villainId);

    public Domain.ValueObjects.Lineup GetVillainLineup(string villainId, Guid playerId) =>
        VillainRegistry.GetLineupForVillain(villainId, playerId);
}
