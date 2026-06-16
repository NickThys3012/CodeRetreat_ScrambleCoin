namespace ScrambleCoin.Domain.Enums;

/// <summary>
/// Represents the mode/type of <see cref="Entities.Game"/>.
/// </summary>
public enum GameMode
{
    /// <summary>Standard 1v1 game between two bots.</summary>
    Standard,

    /// <summary>Solo mode: bot plays against a villain from the unlocked villain tree.</summary>
    Solo
}
