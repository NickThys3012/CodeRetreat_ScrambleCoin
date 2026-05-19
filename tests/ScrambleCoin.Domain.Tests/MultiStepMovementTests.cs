using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Integration tests for multi-step movement sequences (Issue #48).
/// Each of the six special pieces requires multiple move segments to be executed in sequence,
/// with distinct movement constraints for each segment.
/// </summary>
public sealed class MultiStepMovementTests
{
    #region Piece Creation Tests

    [Fact]
    public void Cogsworth_CreatedSuccessfully_HasTwoSegments()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Cogsworth", playerId);
        
        Assert.Equal(2, piece.MovesPerTurn);
        Assert.Equal(2, piece.MovementPatterns.Count);
        Assert.Equal(MovementType.AnyDirection, piece.MovementPatterns[0].MovementType);
        Assert.Equal(1, piece.MovementPatterns[0].MaxDistance);
        Assert.Equal(MovementType.Orthogonal, piece.MovementPatterns[1].MovementType);
        Assert.Equal(2, piece.MovementPatterns[1].MaxDistance);
    }

    [Fact]
    public void Lumiere_CreatedSuccessfully_HasTwoSegments()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Lumiere", playerId);
        
        Assert.Equal(2, piece.MovesPerTurn);
        Assert.Equal(2, piece.MovementPatterns.Count);
        Assert.Equal(MovementType.AnyDirection, piece.MovementPatterns[0].MovementType);
        Assert.Equal(1, piece.MovementPatterns[0].MaxDistance);
        Assert.Equal(MovementType.Diagonal, piece.MovementPatterns[1].MovementType);
        Assert.Equal(2, piece.MovementPatterns[1].MaxDistance);
    }

    [Fact]
    public void Remy_CreatedSuccessfully_HasTwoSegments()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Remy", playerId);
        
        Assert.Equal(2, piece.MovesPerTurn);
        Assert.Equal(2, piece.MovementPatterns.Count);
        Assert.Equal(MovementType.Diagonal, piece.MovementPatterns[0].MovementType);
        Assert.Equal(2, piece.MovementPatterns[0].MaxDistance);
        Assert.Equal(MovementType.Diagonal, piece.MovementPatterns[1].MovementType);
        Assert.Equal(2, piece.MovementPatterns[1].MaxDistance);
    }

    [Fact]
    public void Anna_CreatedSuccessfully_HasThreeSegments()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Anna", playerId);
        
        Assert.Equal(3, piece.MovesPerTurn);
        Assert.Equal(3, piece.MovementPatterns.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(MovementType.Orthogonal, piece.MovementPatterns[i].MovementType);
            Assert.Equal(1, piece.MovementPatterns[i].MaxDistance);
        }
    }

    [Fact]
    public void Olaf_CreatedSuccessfully_HasTwoSegments()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Olaf", playerId);
        
        Assert.Equal(2, piece.MovesPerTurn);
        Assert.Equal(2, piece.MovementPatterns.Count);
        Assert.Equal(MovementType.AnyDirection, piece.MovementPatterns[0].MovementType);
        Assert.Equal(1, piece.MovementPatterns[0].MaxDistance);
        Assert.Equal(MovementType.AnyDirection, piece.MovementPatterns[1].MovementType);
        Assert.Equal(1, piece.MovementPatterns[1].MaxDistance);
    }

    [Fact]
    public void Kristoff_CreatedSuccessfully_HasThreeSegments()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Kristoff", playerId);
        
        Assert.Equal(3, piece.MovesPerTurn);
        Assert.Equal(3, piece.MovementPatterns.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(MovementType.Diagonal, piece.MovementPatterns[i].MovementType);
            Assert.Equal(1, piece.MovementPatterns[i].MaxDistance);
        }
    }

    #endregion

    #region Piece Constructor Validation Tests

    [Fact]
    public void Piece_PatternsCountMismatch_ThrowsException()
    {
        var playerId = Guid.NewGuid();
        var patterns = new[] {
            new MovementPattern(MovementType.Orthogonal, 1)
        };

        var ex = Assert.Throws<DomainException>(() =>
            new Piece(
                "TestPiece",
                playerId,
                EntryPointType.Borders,
                MovementType.Orthogonal,
                2,
                2,
                patterns));

        Assert.Contains("MovementPatterns count (1) must match MovesPerTurn (2)", ex.Message);
    }

    [Fact]
    public void Piece_NoPatterns_CreatesDefaultPatterns()
    {
        var playerId = Guid.NewGuid();
        var piece = new Piece(
            "DefaultPiece",
            playerId,
            EntryPointType.Borders,
            MovementType.Orthogonal,
            3,
            2);

        Assert.Equal(2, piece.MovementPatterns.Count);
        Assert.All(piece.MovementPatterns, p =>
        {
            Assert.Equal(MovementType.Orthogonal, p.MovementType);
            Assert.Equal(3, p.MaxDistance);
        });
    }

    #endregion

    #region Movement Pattern Tests

    [Fact]
    public void MovementPattern_RecordCreated_HasProperValues()
    {
        var pattern = new MovementPattern(MovementType.Diagonal, 2);
        
        Assert.Equal(MovementType.Diagonal, pattern.MovementType);
        Assert.Equal(2, pattern.MaxDistance);
    }

    [Fact]
    public void MovementPattern_MultipleInstances_AreEqual()
    {
        var pattern1 = new MovementPattern(MovementType.Orthogonal, 2);
        var pattern2 = new MovementPattern(MovementType.Orthogonal, 2);
        
        Assert.Equal(pattern1, pattern2);
    }

    #endregion

    #region Single-Step Pieces Backward Compatibility Tests

    [Fact]
    public void Mickey_CreatedWithDefaultPatterns_HasOneSegment()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Mickey", playerId);
        
        Assert.Equal(1, piece.MovesPerTurn);
        Assert.Equal(1, piece.MovementPatterns.Count);
        Assert.Equal(MovementType.Orthogonal, piece.MovementPatterns[0].MovementType);
        Assert.Equal(3, piece.MovementPatterns[0].MaxDistance);
    }

    [Fact]
    public void Minnie_CreatedWithDefaultPatterns_HasOneSegment()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Minnie", playerId);
        
        Assert.Equal(1, piece.MovesPerTurn);
        Assert.Equal(1, piece.MovementPatterns.Count);
        Assert.Equal(MovementType.Diagonal, piece.MovementPatterns[0].MovementType);
        Assert.Equal(3, piece.MovementPatterns[0].MaxDistance);
    }

    [Fact]
    public void Scrooge_CreatedWithDefaultPatterns_HasOneSegment()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Scrooge", playerId);
        
        Assert.Equal(1, piece.MovesPerTurn);
        Assert.Equal(1, piece.MovementPatterns.Count);
        Assert.Equal(MovementType.AnyDirection, piece.MovementPatterns[0].MovementType);
        Assert.Equal(2, piece.MovementPatterns[0].MaxDistance);
    }

    #endregion
}
