namespace ScrambleCoin.Application.Games.Matchmaking;

public sealed record StartMatchResult(
    Guid GameId,
    Guid PlayerOneId,
    Guid PlayerOneToken,
    Guid PlayerTwoId,
    Guid PlayerTwoToken);
