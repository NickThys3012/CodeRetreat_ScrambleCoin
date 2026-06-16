using ScrambleCoin.Application.Games.GetBoardState;

namespace ScrambleCoin.Application.Games.Replay;

/// <summary>
/// A single frame in a game replay — represents the board state at one captured moment.
/// </summary>
/// <param name="SequenceNumber">Monotonically increasing frame index (1-based).</param>
/// <param name="Turn">Turn number when this snapshot was taken.</param>
/// <param name="Phase">Phase name at the time of capture ("PlacePhase", "MovePhase", "CoinSpawn"), or null.</param>
/// <param name="CapturedAt">UTC timestamp when the snapshot was captured.</param>
/// <param name="BoardState">Full board state at this frame.</param>
public sealed record ReplayFrameDto(
    int SequenceNumber,
    int Turn,
    string? Phase,
    DateTimeOffset CapturedAt,
    BoardStateDto BoardState);
