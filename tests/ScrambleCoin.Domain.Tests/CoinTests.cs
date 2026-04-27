using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Domain.Tests;

public class CoinTests
{
    [Fact]
    public void SilverCoin_ShouldHaveValueOne()
    {
        var coin = new Coin(CoinType.Silver);
        Assert.Equal(1, coin.Value);
    }

    [Fact]
    public void GoldCoin_ShouldHaveValueThree()
    {
        var coin = new Coin(CoinType.Gold);
        Assert.Equal(3, coin.Value);
    }

    [Fact]
    public void SilverCoin_ShouldHaveCoinTypeSilver()
    {
        var coin = new Coin(CoinType.Silver);
        Assert.Equal(CoinType.Silver, coin.CoinType);
    }

    [Fact]
    public void GoldCoin_ShouldHaveCoinTypeGold()
    {
        var coin = new Coin(CoinType.Gold);
        Assert.Equal(CoinType.Gold, coin.CoinType);
    }
}
