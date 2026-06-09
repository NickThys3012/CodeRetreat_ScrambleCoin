namespace ScrambleCoin.Application.Games.SoloMode.GetUnlockedPieces;

/// <summary>DTO representing an unlocked or starter piece.</summary>
public sealed record UnlockedPieceDto(
    string PieceId,
    string PieceName,
    PieceSourceEnum Source);
