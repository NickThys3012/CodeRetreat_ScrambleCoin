namespace ScrambleCoin.Domain.Factories;

/// <summary>
/// The canonical list of Disney villains that can appear in the villain tree.
/// </summary>
public static class VillainCatalogue
{
    private static readonly IReadOnlyList<string> _names =
    [
        "Cruella",
        "Elsa",
        "Gaston",
        "Hades",
        "Hans",
        "Jafar",
        "Maleficent",
        "Mother Gothel",
        "Ratcliffe",
        "Scar",
        "Stitch",
        "Stromboli",
        "Tamatoa",
        "Ursula",
        "Yzma"
    ];

    /// <summary>All known villain display names, sorted alphabetically.</summary>
    public static IReadOnlyList<string> AllVillainNames => _names;

    /// <summary>Converts a villain display name to a URL-safe ID slug (e.g. "Mother Gothel" → "mother-gothel").</summary>
    public static string ToId(string villainName) =>
        villainName.ToLowerInvariant().Replace(' ', '-').Replace("•", "");
}
