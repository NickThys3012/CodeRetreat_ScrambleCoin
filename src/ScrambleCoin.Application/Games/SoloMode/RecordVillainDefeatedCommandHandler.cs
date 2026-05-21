using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Games.SoloMode;

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

        // Check if the bot has already defeated this villain
        var alreadyDefeated = await _botUnlocksRepository.HasDefeatedVillainAsync(request.BotId, request.VillainId, cancellationToken);
        if (alreadyDefeated)
        {
            throw new DomainException($"Bot has already defeated villain '{request.VillainId}'.");
        }

        // Record the defeat
        await _botUnlocksRepository.RecordDefeatAsync(
            request.BotId,
            request.VillainId,
            request.UnlockedPieceId,
            cancellationToken);

        // Return the result with a generated unlock ID
        return new RecordVillainDefeatedResult(
            Guid.NewGuid(),
            request.VillainId,
            request.UnlockedPieceId);
    }
}
