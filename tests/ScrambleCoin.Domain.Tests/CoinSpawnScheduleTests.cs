using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Services;

namespace ScrambleCoin.Domain.Tests;

public class CoinSpawnScheduleTests
{
    // ── Turn 1 ────────────────────────────────────────────────────────────────

    [Fact]
    public void For_Turn1_ReturnsBetween9And13SilverCoins()
    {
        var random = new Random(42);

        var coins = CoinSpawnSchedule.For(1, random);

        Assert.InRange(coins.Count, 9, 13);
        Assert.All(coins, c => Assert.Equal(CoinType.Silver, c));
    }

    [Fact]
    public void For_Turn1_AlwaysReturnsAtLeast7Coins()
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            var coins = CoinSpawnSchedule.For(1, new Random(seed));

            Assert.True(coins.Count >= 7,
                $"Seed {seed} produced {coins.Count} coins, expected at least 7.");
        }
    }

    [Fact]
    public void For_Turn1_NeverReturnsMoreThan13Coins()
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            var coins = CoinSpawnSchedule.For(1, new Random(seed));

            Assert.True(coins.Count <= 13,
                $"Seed {seed} produced {coins.Count} coins, expected at most 13.");
        }
    }

    // ── Turn 2 ────────────────────────────────────────────────────────────────

    [Fact]
    public void For_Turn2_ReturnsBetween2And4SilverCoins()
    {
        var random = new Random(42);

        var coins = CoinSpawnSchedule.For(2, random);

        Assert.InRange(coins.Count, 2, 4);
        Assert.All(coins, c => Assert.Equal(CoinType.Silver, c));
    }

    [Fact]
    public void For_Turn2_AllCoinsAreSilver()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var coins = CoinSpawnSchedule.For(2, new Random(seed));

            Assert.All(coins, c => Assert.Equal(CoinType.Silver, c));
        }
    }

    [Fact]
    public void For_Turn2_CountIsAlwaysBetween2And4()
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            var coins = CoinSpawnSchedule.For(2, new Random(seed));
            Assert.InRange(coins.Count, 2, 4);
        }
    }

    // ── Turn 3 ────────────────────────────────────────────────────────────────

    [Fact]
    public void For_Turn3_ReturnsBetween2And4SilverCoins()
    {
        var random = new Random(42);

        var coins = CoinSpawnSchedule.For(3, random);

        Assert.InRange(coins.Count, 2, 4);
        Assert.All(coins, c => Assert.Equal(CoinType.Silver, c));
    }

    [Fact]
    public void For_Turn3_AllCoinsAreSilver()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var coins = CoinSpawnSchedule.For(3, new Random(seed));

            Assert.All(coins, c => Assert.Equal(CoinType.Silver, c));
        }
    }

    [Fact]
    public void For_Turn3_CountIsAlwaysBetween2And4()
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            var coins = CoinSpawnSchedule.For(3, new Random(seed));
            Assert.InRange(coins.Count, 2, 4);
        }
    }

    // ── Turn 4 ────────────────────────────────────────────────────────────────

    [Fact]
    public void For_Turn4_Returns4GoldCoins()
    {
        var random = new Random(42);

        var coins = CoinSpawnSchedule.For(4, random);

        Assert.Equal(4, coins.Count);
        Assert.All(coins, c => Assert.Equal(CoinType.Gold, c));
    }

    [Fact]
    public void For_Turn4_CountIsAlwaysExactly4()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var coins = CoinSpawnSchedule.For(4, new Random(seed));

            Assert.Equal(4, coins.Count);
        }
    }

    // ── Turn 5 ────────────────────────────────────────────────────────────────

    [Fact]
    public void For_Turn5_Returns3GoldCoins()
    {
        var random = new Random(42);

        var coins = CoinSpawnSchedule.For(5, random);

        Assert.Equal(3, coins.Count);
        Assert.All(coins, c => Assert.Equal(CoinType.Gold, c));
    }

    [Fact]
    public void For_Turn5_CountIsAlwaysExactly3()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var coins = CoinSpawnSchedule.For(5, new Random(seed));

            Assert.Equal(3, coins.Count);
        }
    }

    // ── Invalid turns ─────────────────────────────────────────────────────────

    [Fact]
    public void For_Turn0_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CoinSpawnSchedule.For(0, new Random()));
    }

    [Fact]
    public void For_Turn6_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CoinSpawnSchedule.For(6, new Random()));
    }

    [Fact]
    public void For_NegativeTurn_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CoinSpawnSchedule.For(-1, new Random()));
    }
}
