namespace ScrambleCoin.Application.Games.SoloMode.GetVillainPath;

/// <summary>Status of a villain node for a specific bot.</summary>
public enum VillainStatusEnum
{
    /// <summary>Villain is available to challenge (root or parent defeated).</summary>
    Available,

    /// <summary>Villain cannot be challenged yet (parent not defeated).</summary>
    Locked,

    /// <summary>Bot has already defeated this villain.</summary>
    Defeated
}
