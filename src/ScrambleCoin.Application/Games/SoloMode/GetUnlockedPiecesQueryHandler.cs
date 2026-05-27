using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Factories;

namespace ScrambleCoin.Application.Games.SoloMode;

/// <summary>
/// Handles <see cref="GetUnlockedPiecesQuery"/>.
/// Returns all pieces available to a bot: starter pieces and pieces unlocked from defeats.
/// </summary>
public sealed class GetUnlockedPiecesQueryHandler : IRequestHandler<GetUnlockedPiecesQuery, GetUnlockedPiecesQueryResult>
{
    private readonly IBotUnlocksRepository _botUnlocksRepository;

    public GetUnlockedPiecesQueryHandler(IBotUnlocksRepository botUnlocksRepository)
    {
        _botUnlocksRepository = botUnlocksRepository;
    }

    public async Task<GetUnlockedPiecesQueryResult> Handle(GetUnlockedPiecesQuery request, CancellationToken cancellationToken)
    {
        var pieces = new List<UnlockedPieceDto>();

        // Add starter pieces
        var starterPieces = PieceFactory.GetStarterPieces();
        foreach (var pieceName in starterPieces)
        {
            pieces.Add(new UnlockedPieceDto(
                pieceName.ToLower(),
                pieceName,
                PieceSourceEnum.Starter));
        }

        // Add defeated villain rewards
        var defeatedVillains = await _botUnlocksRepository.GetDefeatedVillainsAsync(request.BotId, cancellationToken);
        foreach (var defeat in defeatedVillains)
        {
            if (!string.IsNullOrEmpty(defeat.UnlockedPieceId))
            {
                pieces.Add(new UnlockedPieceDto(
                    defeat.UnlockedPieceId.ToLower(),
                    defeat.UnlockedPieceId,
                    PieceSourceEnum.DefeatedVillain));
            }
        }

        return new GetUnlockedPiecesQueryResult(pieces.AsReadOnly());
    }
}
