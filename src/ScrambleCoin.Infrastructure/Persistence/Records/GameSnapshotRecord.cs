namespace ScrambleCoin.Infrastructure.Persistence.Records;

/// <summary>
/// EF Core persistence record for a single replay snapshot frame.
/// One row is written after every board broadcast (move, placement, coin spawn).
/// </summary>
public sealed class GameSnapshotRecord
{
    public long Id { get; set; }
    public Guid GameId { get; set; }
    public int SequenceNumber { get; set; }
    public int Turn { get; set; }
    public string? Phase { get; set; }
    /// <summary>JSON: serialised <see cref="ScrambleCoin.Application.Games.GetBoardState.BoardStateDto"/>.</summary>
    public string BoardStateJson { get; set; } = "{}";
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
