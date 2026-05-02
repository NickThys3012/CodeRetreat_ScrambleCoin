namespace ScrambleCoin.Infrastructure.Persistence.Records;

/// <summary>
/// EF Core persistence POCO for the <see cref="ScrambleCoin.Domain.BotRegistrations.BotRegistration"/> entity.
/// </summary>
public sealed class BotRegistrationRecord
{
    /// <summary>Bearer token — primary key.</summary>
    public Guid Token { get; set; }

    /// <summary>The player slot ID assigned to this bot in the game.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>The game this bot is registered to.</summary>
    public Guid GameId { get; set; }
}
