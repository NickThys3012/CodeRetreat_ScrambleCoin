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

    /// <summary>
    /// Stages a bot registration for persistence without committing to the store.
    /// The caller is responsible for calling <see cref="IUnitOfWork.SaveChangesAsync"/>
    /// to flush all staged changes in a single transaction.
    /// </summary>
    Task StageAsync(DomainBotReg botRegistration, CancellationToken cancellationToken = default);
}
