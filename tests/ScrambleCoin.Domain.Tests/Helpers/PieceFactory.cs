using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Domain.Tests.Helpers;

/// <summary>
/// Test helper that creates <see cref="Piece"/> instances with sensible defaults,
/// replacing the removed <c>Piece(string name)</c> convenience constructor.
/// </summary>
internal static class PieceFactory
{
    /// <summary>
    /// Creates a fully valid <see cref="Piece"/> with sensible test defaults.
    /// </summary>
    /// <param name="name">Display name for the piece. Defaults to "TestPiece".</param>
    internal static Piece Any(string name = "TestPiece") =>
        new(
            id: Guid.NewGuid(),
            name: name,
            playerId: Guid.NewGuid(),
            entryPointType: EntryPointType.Borders,
            movementType: MovementType.Orthogonal,
            maxDistance: 3,
            movesPerTurn: 1);
}
