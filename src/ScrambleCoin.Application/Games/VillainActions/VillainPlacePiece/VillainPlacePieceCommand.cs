using MediatR;
using ScrambleCoin.Domain.ValueObjects;
namespace ScrambleCoin.Application.Games.VillainActions.VillainPlacePiece;

/// <summary>
/// Command for the villain to place a piece during PlacePhase.
/// This is similar to SubmitPlacementCommand but takes the player ID directly instead of a bot token.
/// </summary>
public sealed record VillainPlacePieceCommand(
    Guid GameId,
    Guid VillainPlayerId,
    Guid PieceId,
    Position Position) : IRequest<VillainPlacementResultDto>, IGameStateChangingCommand;
    
