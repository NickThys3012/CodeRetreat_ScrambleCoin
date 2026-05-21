using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Services.Villains;

/// <summary>
/// Registry of predefined villain identities with their hardcoded lineups.
/// Each villain has a unique ID, name, and a set of 5 pieces in their lineup.
/// </summary>
public static class VillainRegistry
{
    /// <summary>Elsa villain with her themed lineup.</summary>
    public static class Elsa
    {
        public const string Id = "elsa";
        public const string DisplayName = "Elsa";

        /// <summary>Gets Elsa's hardcoded 5-piece lineup.</summary>
        public static Lineup GetLineup(Guid playerId) =>
            new([
                PieceFactory.Create("Mickey", playerId),
                PieceFactory.Create("Donald", playerId),
                PieceFactory.Create("WALL•E", playerId),
                PieceFactory.Create("Merlin", playerId),
                PieceFactory.Create("Scrooge", playerId)
            ]);
    }

    /// <summary>Ursula villain with her themed lineup.</summary>
    public static class Ursula
    {
        public const string Id = "ursula";
        public const string DisplayName = "Ursula";

        /// <summary>Gets Ursula's hardcoded 5-piece lineup.</summary>
        public static Lineup GetLineup(Guid playerId) =>
            new([
                PieceFactory.Create("Mickey", playerId),
                PieceFactory.Create("Donald", playerId),
                PieceFactory.Create("Anna", playerId),
                PieceFactory.Create("Goofy", playerId),
                PieceFactory.Create("Cinderella", playerId)
            ]);
    }

    /// <summary>Gaston villain with a balanced lineup.</summary>
    public static class Gaston
    {
        public const string Id = "gaston";
        public const string DisplayName = "Gaston";

        /// <summary>Gets Gaston's hardcoded 5-piece lineup (sensible defaults since not documented).</summary>
        public static Lineup GetLineup(Guid playerId) =>
            new([
                PieceFactory.Create("Minnie", playerId),
                PieceFactory.Create("Goofy", playerId),
                PieceFactory.Create("Donald", playerId),
                PieceFactory.Create("Scrooge", playerId),
                PieceFactory.Create("Mickey", playerId)
            ]);
    }

    /// <summary>
    /// Gets the lineup for a villain by ID, or throws if the villain is not recognized.
    /// </summary>
    public static Lineup GetLineupForVillain(string villainId, Guid playerId) =>
        villainId switch
        {
            Elsa.Id => Elsa.GetLineup(playerId),
            Ursula.Id => Ursula.GetLineup(playerId),
            Gaston.Id => Gaston.GetLineup(playerId),
            _ => throw new ArgumentException($"Unknown villain ID: {villainId}", nameof(villainId))
        };

    /// <summary>
    /// Gets the display name for a villain by ID.
    /// </summary>
    public static string GetDisplayName(string villainId) =>
        villainId switch
        {
            Elsa.Id => Elsa.DisplayName,
            Ursula.Id => Ursula.DisplayName,
            Gaston.Id => Gaston.DisplayName,
            _ => throw new ArgumentException($"Unknown villain ID: {villainId}", nameof(villainId))
        };
}
