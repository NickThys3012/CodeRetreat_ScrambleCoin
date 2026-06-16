using MediatR;

namespace ScrambleCoin.Application.Games.Replay;

/// <summary>Returns all replay frames for a completed (or in-progress) game, ordered by sequence number.</summary>
public sealed record GetGameReplayQuery(Guid GameId) : IRequest<IReadOnlyList<ReplayFrameDto>>;
