using MediatR;
using ScrambleCoin.Domain.Factories;
namespace ScrambleCoin.Application.Games.SoloMode.GetStarterPieces;

/// <summary>
/// Handles <see cref="GetStarterPiecesQuery"/>.
/// Returns the hardcoded list of starter pieces available to all bots.
/// </summary>
public sealed class GetStarterPiecesQueryHandler : IRequestHandler<GetStarterPiecesQuery, GetStarterPiecesDto>
{
    public Task<GetStarterPiecesDto> Handle(GetStarterPiecesQuery request, CancellationToken cancellationToken)
    {
        var starterPieces = PieceFactory.GetStarterPieces()
            .Select(p => p.ToLower())
            .ToList()
            .AsReadOnly();

        return Task.FromResult(new GetStarterPiecesDto(starterPieces));
    }
}
