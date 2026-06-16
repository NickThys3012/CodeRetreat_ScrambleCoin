using MediatR;
namespace ScrambleCoin.Application.Games.SoloMode.GetStarterPieces;

/// <summary>
/// Query to get the default starter pieces available to all bots.
/// </summary>
public sealed record GetStarterPiecesQuery : IRequest<GetStarterPiecesDto>;
