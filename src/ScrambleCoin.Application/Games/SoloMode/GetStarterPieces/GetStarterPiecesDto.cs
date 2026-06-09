namespace ScrambleCoin.Application.Games.SoloMode.GetStarterPieces;

/// <summary>Result of <see cref="GetStarterPiecesQuery"/>.</summary>
public sealed record GetStarterPiecesDto(IReadOnlyList<string> StarterPieceIds);
