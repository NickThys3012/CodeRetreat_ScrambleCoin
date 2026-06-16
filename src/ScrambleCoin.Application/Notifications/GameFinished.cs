using MediatR;

namespace ScrambleCoin.Application.Notifications;

/// <summary>
/// Notification published when a game finishes.
/// </summary>
public sealed record GameFinished(
    Guid GameId,
    Guid? WinnerId,
    bool IsDraw) : INotification;
