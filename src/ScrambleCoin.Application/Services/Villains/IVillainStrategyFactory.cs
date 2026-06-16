using ScrambleCoin.Application.Services.Villains.Implementations;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Services.Villains;

/// <summary>
/// Factory that resolves a villain ID to its AI strategy, display name, and hardcoded lineup.
/// </summary>
public interface IVillainStrategyFactory
{
    /// <summary>Creates the villain AI strategy for the given villain ID.</summary>
    IVillainStrategy CreateStrategy(string villainId);

    /// <summary>Gets the display name for the given villain ID.</summary>
    string GetDisplayName(string villainId);

    /// <summary>Gets the villain's hardcoded 5-piece lineup, owned by <paramref name="playerId"/>.</summary>
    Lineup GetVillainLineup(string villainId, Guid playerId);
}

/// <summary>
/// Default implementation of <see cref="IVillainStrategyFactory"/>.
/// </summary>
public sealed class VillainStrategyFactory : IVillainStrategyFactory
{
    /// <inheritdoc/>
    public IVillainStrategy CreateStrategy(string villainId) =>
        VillainRegistry.Normalize(villainId) switch
        {
            VillainRegistry.Elsa.Id => new ElsaStrategy(),
            VillainRegistry.Ursula.Id => new UrsulaStrategy(),
            VillainRegistry.Gaston.Id => new GastonStrategy(),
            _ => throw new DomainException($"Unknown villain ID: {villainId}")
        };

    /// <inheritdoc/>
    public string GetDisplayName(string villainId) =>
        VillainRegistry.GetDisplayName(villainId);

    /// <inheritdoc/>
    public Lineup GetVillainLineup(string villainId, Guid playerId) =>
        VillainRegistry.GetLineupForVillain(villainId, playerId);
}
