using MediatR;

namespace ScrambleCoin.Application.Games.SoloMode;

/// <summary>
/// Query to get all pieces available to a bot (starter pieces and pieces unlocked from defeats).
/// </summary>
public sealed record GetUnlockedPiecesQuery(Guid BotId) : IRequest<GetUnlockedPiecesQueryResult>;

/// <summary>Result of <see cref="GetUnlockedPiecesQuery"/>.</summary>
public sealed record GetUnlockedPiecesQueryResult(IReadOnlyList<UnlockedPieceDto> Pieces);

/// <summary>DTO representing an unlocked or starter piece.</summary>
public sealed record UnlockedPieceDto(
    string PieceId,
    string PieceName,
    PieceSourceEnum Source);

/// <summary>Where a piece came from.</summary>
public enum PieceSourceEnum
{
    /// <summary>One of the default starter pieces.</summary>
    Starter,

    /// <summary>Unlocked by defeating a villain.</summary>
    DefeatedVillain
}
