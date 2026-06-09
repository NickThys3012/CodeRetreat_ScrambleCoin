using MediatR;
namespace ScrambleCoin.Application.Games.SoloMode.GetUnlockedPieces;

/// <summary>
/// Query to get all pieces available to a bot (starter pieces and pieces unlocked from defeats).
/// </summary>
public sealed record GetUnlockedPiecesQuery(Guid BotId) : IRequest<GetUnlockedPiecesQueryResult>;

/// <summary>Result of <see cref="GetUnlockedPiecesQuery"/>.</summary>
public sealed record GetUnlockedPiecesQueryResult(IReadOnlyList<UnlockedPieceDto> Pieces);
