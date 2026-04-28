using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

public class TileTests
{
    private static Tile MakeTile() => new Tile(new Position(0, 0));

    [Fact]
    public void NewTile_ShouldBeEmpty()
    {
        var tile = MakeTile();
        Assert.True(tile.IsEmpty);
    }

    [Fact]
    public void NewTile_AsCoin_ShouldBeNull()
    {
        var tile = MakeTile();
        Assert.Null(tile.AsCoin);
    }

    [Fact]
    public void NewTile_AsPiece_ShouldBeNull()
    {
        var tile = MakeTile();
        Assert.Null(tile.AsPiece);
    }

    [Fact]
    public void SetOccupant_WithCoin_AsCoin_ShouldReturnCoin()
    {
        var tile = MakeTile();
        var coin = new Coin(CoinType.Silver);
        tile.SetOccupant(coin);
        Assert.Equal(coin, tile.AsCoin);
    }

    [Fact]
    public void SetOccupant_WithCoin_AsPiece_ShouldBeNull()
    {
        var tile = MakeTile();
        tile.SetOccupant(new Coin(CoinType.Silver));
        Assert.Null(tile.AsPiece);
    }

    [Fact]
    public void SetOccupant_WithCoin_IsEmpty_ShouldBeFalse()
    {
        var tile = MakeTile();
        tile.SetOccupant(new Coin(CoinType.Gold));
        Assert.False(tile.IsEmpty);
    }

    [Fact]
    public void SetOccupant_WithPiece_AsPiece_ShouldReturnPiece()
    {
        var tile = MakeTile();
        var piece = PieceFactory.Any("Alice");
        tile.SetOccupant(piece);
        Assert.Equal(piece, tile.AsPiece);
    }

    [Fact]
    public void SetOccupant_WithPiece_AsCoin_ShouldBeNull()
    {
        var tile = MakeTile();
        tile.SetOccupant(PieceFactory.Any("Alice"));
        Assert.Null(tile.AsCoin);
    }

    [Fact]
    public void ClearOccupant_AfterSettingCoin_ShouldBeEmpty()
    {
        var tile = MakeTile();
        tile.SetOccupant(new Coin(CoinType.Silver));
        tile.ClearOccupant();
        Assert.True(tile.IsEmpty);
    }

    [Fact]
    public void ClearOccupant_AfterSettingCoin_AsCoin_ShouldBeNull()
    {
        var tile = MakeTile();
        tile.SetOccupant(new Coin(CoinType.Silver));
        tile.ClearOccupant();
        Assert.Null(tile.AsCoin);
    }

    [Fact]
    public void SetOccupant_WithInvalidObject_ShouldThrowArgumentException()
    {
        var tile = MakeTile();
        Assert.Throws<ArgumentException>(() => tile.SetOccupant("not a valid occupant"));
    }

    [Fact]
    public void SetOccupant_WithInvalidObject_Int_ShouldThrowArgumentException()
    {
        var tile = MakeTile();
        Assert.Throws<ArgumentException>(() => tile.SetOccupant(42));
    }

    [Fact]
    public void SetOccupant_WithNull_ShouldSetEmpty()
    {
        var tile = MakeTile();
        tile.SetOccupant(new Coin(CoinType.Silver));
        tile.SetOccupant(null);
        Assert.True(tile.IsEmpty);
    }

    [Fact]
    public void Position_ShouldMatchConstructorArgument()
    {
        var pos = new Position(4, 5);
        var tile = new Tile(pos);
        Assert.Equal(pos, tile.Position);
    }
}
