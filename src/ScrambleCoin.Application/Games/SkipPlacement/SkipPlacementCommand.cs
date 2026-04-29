using MediatR;

namespace ScrambleCoin.Application.Games.SkipPlacement;

/// <summary>Skips the placement action for a player during the PlacePhase.</summary>
public sealed record SkipPlacementCommand(Guid GameId, Guid PlayerId) : IRequest;
