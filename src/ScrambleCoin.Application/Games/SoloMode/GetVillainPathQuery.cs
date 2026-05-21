using MediatR;

namespace ScrambleCoin.Application.Games.SoloMode;

/// <summary>
/// Query to get the villain unlock tree for a specific bot, showing which villains are available,
/// locked, or already defeated.
/// </summary>
public sealed record GetVillainPathQuery(Guid BotId) : IRequest<GetVillainPathQueryResult>;

/// <summary>Result of <see cref="GetVillainPathQuery"/>.</summary>
public sealed record GetVillainPathQueryResult(IReadOnlyList<VillainNodeDto> Nodes);

/// <summary>DTO representing a villain node in the tree.</summary>
public sealed record VillainNodeDto(
    string VillainId,
    string Name,
    VillainStatusEnum Status,
    PieceDto? UnlockedPiece,
    IReadOnlyList<string> ChildrenVillainIds);

/// <summary>Status of a villain node for a specific bot.</summary>
public enum VillainStatusEnum
{
    /// <summary>Villain is available to challenge (root or parent defeated).</summary>
    Available,

    /// <summary>Villain cannot be challenged yet (parent not defeated).</summary>
    Locked,

    /// <summary>Bot has already defeated this villain.</summary>
    Defeated
}

/// <summary>DTO representing a piece (starter or unlocked).</summary>
public sealed record PieceDto(string Id, string Name);
