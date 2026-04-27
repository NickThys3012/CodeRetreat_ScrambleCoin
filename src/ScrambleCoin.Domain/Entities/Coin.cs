using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// A coin that can occupy a tile. Has a type (Silver or Gold) and a point value.
/// </summary>
public sealed class Coin
{
    public CoinType CoinType { get; }

    /// <summary>Point value of this coin (Silver = 1, Gold = 3).</summary>
    public int Value => (int)CoinType;

    public Coin(CoinType coinType)
    {
        CoinType = coinType;
    }
}
