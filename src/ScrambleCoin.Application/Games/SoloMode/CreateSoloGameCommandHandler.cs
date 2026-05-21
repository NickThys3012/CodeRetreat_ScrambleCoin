using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Games.SoloMode;

/// <summary>
/// Handles <see cref="CreateSoloGameCommand"/>.
/// Creates a solo game between a bot and a villain, validating that the villain is available (not locked).
/// </summary>
public sealed class CreateSoloGameCommandHandler : IRequestHandler<CreateSoloGameCommand, CreateSoloGameResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IVillainTreeRepository _villainTreeRepository;
    private readonly IBotUnlocksRepository _botUnlocksRepository;
    private readonly ILogger<CreateSoloGameCommandHandler> _logger;

    public CreateSoloGameCommandHandler(
        IGameRepository gameRepository,
        IVillainTreeRepository villainTreeRepository,
        IBotUnlocksRepository botUnlocksRepository,
        ILogger<CreateSoloGameCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _villainTreeRepository = villainTreeRepository;
        _botUnlocksRepository = botUnlocksRepository;
        _logger = logger;
    }

    public async Task<CreateSoloGameResult> Handle(CreateSoloGameCommand request, CancellationToken cancellationToken)
    {
        // Verify the villain exists
        var villain = await _villainTreeRepository.GetNodeByVillainIdAsync(request.VillainId, cancellationToken);
        if (villain == null)
        {
            throw new DomainException($"Villain '{request.VillainId}' not found.");
        }

        // Check if the villain is available (not locked)
        var defeatedVillains = await _botUnlocksRepository.GetDefeatedVillainsAsync(request.BotId, cancellationToken);
        var defeatedVillainIds = defeatedVillains.Select(d => d.VillainId).ToHashSet();

        var isLocked = villain.RequiredParentVillainId != null && 
                       !defeatedVillainIds.Contains(villain.RequiredParentVillainId);

        if (isLocked)
        {
            throw new DomainException(
                $"Villain '{request.VillainId}' is locked. Defeat parent villain '{villain.RequiredParentVillainId}' first.");
        }

        // Check if the bot has already defeated this villain
        var alreadyDefeated = defeatedVillainIds.Contains(request.VillainId);
        if (alreadyDefeated)
        {
            throw new DomainException($"Villain '{request.VillainId}' has already been defeated.");
        }

        // Generate a deterministic villain "player" ID based on the villain name
        var villainPlayerId = GenerateVillainPlayerId(request.VillainId);

        // Create the game
        var board = new Board();
        var game = new Game(Guid.NewGuid(), request.BotId, villainPlayerId, board)
        {
            GameMode = GameMode.Solo,
            VillainId = request.VillainId
        };

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Solo game created: GameId={GameId}, BotId={BotId}, VillainId={VillainId}",
            game.Id, request.BotId, request.VillainId);

        return new CreateSoloGameResult(game.Id, request.VillainId);
    }

    /// <summary>
    /// Generates a deterministic villain player ID from a villain ID.
    /// This ensures the same villain always has the same player ID.
    /// </summary>
    private static Guid GenerateVillainPlayerId(string villainId)
    {
        // Use a namespace UUID based on a fixed UUID for "villain"
        var villainNamespaceId = new Guid("00000000-0000-0000-0000-000000000001");
        return GuidV5(villainNamespaceId, villainId);
    }

    /// <summary>Generates a version 5 (SHA-1 name-based) UUID.</summary>
    private static Guid GuidV5(Guid namespaceId, string name)
    {
        // This is a simplified V5 UUID implementation
        // In production, consider using a library or the standard .NET implementation
        using (var sha1 = System.Security.Cryptography.SHA1.Create())
        {
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
            var namespaceBytes = namespaceId.ToByteArray();
            
            // Combine namespace and name
            var data = new byte[namespaceBytes.Length + nameBytes.Length];
            System.Buffer.BlockCopy(namespaceBytes, 0, data, 0, namespaceBytes.Length);
            System.Buffer.BlockCopy(nameBytes, 0, data, namespaceBytes.Length, nameBytes.Length);

            var hash = sha1.ComputeHash(data);

            // Set version to 5 and variant to RFC 4122
            hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

            return new Guid(hash.Take(16).ToArray());
        }
    }
}
