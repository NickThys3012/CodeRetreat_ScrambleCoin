using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot;

/// <summary>
/// Strategy interface. Replace the default <see cref="GreedyStrategy"/> with your own
/// implementation to change how the bot decides where to place and move pieces.
/// </summary>
public interface IStrategy
{
    /// <summary>
    /// Decide what placement action to take during <c>PlacePhase</c>.
    /// Called once per piece that is not yet on the board.
    /// </summary>
    /// <param name="state">The current board state (relative to your bot).</param>
    /// <param name="piece">The piece that needs to be placed.</param>
    /// <returns>
    /// A <see cref="PlacementDecision.Place"/> to place the piece, or
    /// <see cref="PlacementDecision.Skip"/> to skip placement for this turn.
    /// </returns>
    PlacementDecision DecidePlacement(BoardState state, PieceState piece);

    /// <summary>
    /// Decide how to move a piece during <c>MovePhase</c>.
    /// Called once per placed piece when it is your turn.
    /// </summary>
    /// <param name="state">The current board state (relative to your bot).</param>
    /// <param name="piece">The piece to move.</param>
    /// <returns>
    /// A <see cref="MoveDecision"/> containing one segment per <c>MovesPerTurn</c>.
    /// Each segment lists the positions the piece steps through (empty = stay still).
    /// </returns>
    MoveDecision DecideMove(BoardState state, PieceState piece);
}
