namespace ScrambleCoin.Application.Games.MovePiece;

/// <summary>Returned by <see cref="MovePieceCommand"/> after a piece is successfully moved.</summary>
/// <param name="Phase">Current phase after the move, or null if game ended.</param>
/// <param name="ActivePlayer">Next player to move, or null if MovePhase ended.</param>
/// <param name="YourScore">Score of the bot that submitted the move.</param>
/// <param name="OpponentScore">Score of the opposing player.</param>
public sealed record MoveResult(string? Phase, Guid? ActivePlayer, int YourScore, int OpponentScore);
