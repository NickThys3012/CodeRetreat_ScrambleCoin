namespace ScrambleCoin.Application.Games;

/// <summary>
/// Marker interface for MediatR commands that mutate game state and should trigger
/// a SignalR board-state broadcast after successful execution.
/// Apply this to commands such as <c>MovePieceCommand</c> and <c>SubmitPlacementCommand</c>.
/// </summary>
public interface IGameStateChangingCommand
{
    /// <summary>The game identifier that the command targets.</summary>
    Guid GameId { get; }
}
