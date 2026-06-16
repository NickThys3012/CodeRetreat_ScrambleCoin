using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Services.Villains;

/// <summary>
/// Registry of the predefined villain identities and their hardcoded 5-piece lineups.
/// Villain IDs use the same slug format produced by <see cref="VillainCatalogue.ToId"/>
/// (e.g. "Elsa" → "elsa"), matching the <c>VillainId</c> stored on solo games.
/// </summary>
public static class VillainRegistry
{
    /// <summary>Elsa villain with her themed lineup.</summary>
    public static class Elsa
    {
        public const string Id = "elsa";
        public const string DisplayName = "Elsa";

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

        public static Lineup GetLineup(Guid playerId) =>
            new([
                PieceFactory.Create("Mickey", playerId),
                PieceFactory.Create("Donald", playerId),
                PieceFactory.Create("Anna", playerId),
                PieceFactory.Create("Goofy", playerId),
                PieceFactory.Create("Cinderella", playerId)
            ]);
    }

    /// <summary>Gaston villain with his themed lineup.</summary>
    public static class Gaston
    {
        public const string Id = "gaston";
        public const string DisplayName = "Gaston";

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
    /// Generic fallback lineup used for villains that do not yet have a bespoke lineup defined.
    /// Ensures any solo game can still be created and started; such villains simply use this lineup
    /// (and, lacking a bespoke strategy, are driven by the default greedy behaviour or remain idle).
    /// </summary>
    public static Lineup GetDefaultLineup(Guid playerId) =>
        new([
            PieceFactory.Create("Mickey", playerId),
            PieceFactory.Create("Minnie", playerId),
            PieceFactory.Create("Donald", playerId),
            PieceFactory.Create("Goofy", playerId),
            PieceFactory.Create("Scrooge", playerId)
        ]);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="villainId"/> matches one of the registered villains.
    /// </summary>
    public static bool IsKnown(string villainId) => Normalize(villainId) is Elsa.Id or Ursula.Id or Gaston.Id;

    /// <summary>
    /// Gets the bespoke lineup for a known villain, or the <see cref="GetDefaultLineup"/> otherwise.
    /// Never throws, so solo-game creation works for every villain in the tree.
    /// </summary>
    public static Lineup GetLineupForVillainOrDefault(string villainId, Guid playerId) =>
        IsKnown(villainId)
            ? GetLineupForVillain(villainId, playerId)
            : GetDefaultLineup(playerId);

    /// <summary>
    /// Gets the hardcoded lineup for a villain by ID, or throws if the villain is not recognised.
    /// Tolerant of the actual slug format used by created solo games (case/spacing/diacritics).
    /// </summary>
    public static Lineup GetLineupForVillain(string villainId, Guid playerId) =>
        Normalize(villainId) switch
        {
            Elsa.Id => Elsa.GetLineup(playerId),
            Ursula.Id => Ursula.GetLineup(playerId),
            Gaston.Id => Gaston.GetLineup(playerId),
            _ => throw new ArgumentException($"Unknown villain ID: {villainId}", nameof(villainId))
        };

    /// <summary>
    /// Gets the display name for a villain by ID, or throws if the villain is not recognised.
    /// </summary>
    public static string GetDisplayName(string villainId) =>
        Normalize(villainId) switch
        {
            Elsa.Id => Elsa.DisplayName,
            Ursula.Id => Ursula.DisplayName,
            Gaston.Id => Gaston.DisplayName,
            _ => throw new ArgumentException($"Unknown villain ID: {villainId}", nameof(villainId))
        };

    /// <summary>
    /// Normalises an arbitrary villain identifier to the canonical slug form
    /// (e.g. "Elsa", "ELSA", "elsa" all map to "elsa").
    /// </summary>
    public static string Normalize(string villainId) =>
        VillainCatalogue.ToId((villainId ?? string.Empty).Trim());
}
