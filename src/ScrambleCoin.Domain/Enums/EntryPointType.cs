namespace ScrambleCoin.Domain.Enums;

/// <summary>
/// Determines which tiles a piece may use when entering the board.
/// </summary>
public enum EntryPointType
{
    /// <summary>The piece may be placed on any edge tile of the board.</summary>
    Borders,

    /// <summary>The piece may only be placed on one of the 4 corner tiles.</summary>
    Corners,

    /// <summary>The piece may be placed on any free tile on the board.</summary>
    Anywhere
}
