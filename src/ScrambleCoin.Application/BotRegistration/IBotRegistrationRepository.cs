using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.BotRegistration;

/// <summary>
/// Persistence operations for <see cref="DomainBotReg"/> entities.
/// </summary>
public interface IBotRegistrationRepository
{
    /// <summary>Retrieves a bot registration by its bearer token.</summary>
    /// <returns>The matching registration, or <c>null</c> if not found.</returns>
    Task<DomainBotReg?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default);

    /// <summary>Persists a new bot registration.</summary>
    Task SaveAsync(DomainBotReg botRegistration, CancellationToken cancellationToken = default);
}
