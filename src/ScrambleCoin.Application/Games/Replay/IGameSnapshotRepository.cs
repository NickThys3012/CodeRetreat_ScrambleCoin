namespace ScrambleCoin.Application.Games.Replay;

/// <summary>
/// Repository for reading and writing game replay snapshots.
/// Implemented in Infrastructure; registered as a scoped service.
/// </summary>
public interface IGameSnapshotRepository
{
    /// <summary>Persists a new snapshot frame for the given game.</summary>
    Task SaveSnapshotAsync(Guid gameId, int turn, string? phase, string boardStateJson, CancellationToken ct = default);

    /// <summary>Returns all frames for the given game, ordered ascending by sequence number.</summary>
    Task<IReadOnlyList<ReplayFrameDto>> GetFramesAsync(Guid gameId, CancellationToken ct = default);
}
