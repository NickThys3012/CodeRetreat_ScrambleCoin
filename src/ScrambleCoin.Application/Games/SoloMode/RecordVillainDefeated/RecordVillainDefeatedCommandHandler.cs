using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Exceptions;
namespace ScrambleCoin.Application.Games.SoloMode.RecordVillainDefeated;

/// <summary>
/// Handles <see cref="RecordVillainDefeatedCommand"/>.
/// Records a bot victory over a villain and unlocks any associated piece reward.
/// </summary>
public sealed class RecordVillainDefeatedCommandHandler : IRequestHandler<RecordVillainDefeatedCommand, RecordVillainDefeatedResult>
{
    private readonly IBotUnlocksRepository _botUnlocksRepository;
    private readonly IVillainTreeRepository _villainTreeRepository;

    public RecordVillainDefeatedCommandHandler(
        IBotUnlocksRepository botUnlocksRepository,
        IVillainTreeRepository villainTreeRepository)
    {
        _botUnlocksRepository = botUnlocksRepository;
        _villainTreeRepository = villainTreeRepository;
    }

    public async Task<RecordVillainDefeatedResult> Handle(RecordVillainDefeatedCommand request, CancellationToken cancellationToken)
    {
        // Verify the villain exists
        var villain = await _villainTreeRepository.GetNodeByVillainIdAsync(request.VillainId, cancellationToken);
        if (villain == null)
        {
            throw new DomainException($"Villain '{request.VillainId}' not found.");
        }

        // Record the defeat (UPSERT: allows re-challenging the same villain)
        await _botUnlocksRepository.RecordDefeatAsync(
            request.BotId,
            request.VillainId,
            request.UnlockedPieceId,
            cancellationToken);

        // Return the result with a generated unlocked ID
        return new RecordVillainDefeatedResult(
            Guid.NewGuid(),
            request.VillainId,
            request.UnlockedPieceId);
    }
}
