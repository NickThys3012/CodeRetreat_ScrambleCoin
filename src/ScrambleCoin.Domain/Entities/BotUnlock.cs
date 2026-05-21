namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Records when a bot defeats a villain and unlocks a piece reward.
/// Multiple records can exist per (BotId, VillainId) pair to support re-challenging.
/// </summary>
public sealed class BotUnlock
{
    /// <summary>Unique identifier for this unlock record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The ID of the bot that achieved this unlock.</summary>
    public Guid BotId { get; set; }

    /// <summary>The ID of the villain that was defeated.</summary>
    public string VillainId { get; set; } = null!;

    /// <summary>
    /// The ID of the piece unlocked by defeating this villain.
    /// Null if the villain awards no piece.
    /// </summary>
    public string? UnlockedPieceId { get; set; }

    /// <summary>Timestamp when this villain was defeated.</summary>
    public DateTime DefeatedAtUtc { get; set; } = DateTime.UtcNow;
}
