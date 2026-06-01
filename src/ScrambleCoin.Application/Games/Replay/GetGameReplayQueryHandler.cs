using MediatR;

namespace ScrambleCoin.Application.Games.Replay;

public sealed class GetGameReplayQueryHandler : IRequestHandler<GetGameReplayQuery, IReadOnlyList<ReplayFrameDto>>
{
    private readonly IGameSnapshotRepository _snapshots;

    public GetGameReplayQueryHandler(IGameSnapshotRepository snapshots)
    {
        _snapshots = snapshots;
    }

    public async Task<IReadOnlyList<ReplayFrameDto>> Handle(GetGameReplayQuery request, CancellationToken cancellationToken)
        => await _snapshots.GetFramesAsync(request.GameId, cancellationToken);
}
