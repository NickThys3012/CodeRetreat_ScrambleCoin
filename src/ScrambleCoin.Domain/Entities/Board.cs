using ScrambleCoin.Domain.Obstacles;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Holds the result of <see cref="Board.GetAllObstacles"/>.
/// </summary>
public sealed record BoardObstacles(
    IReadOnlyList<Rock> Rocks,
    IReadOnlyList<Lake> Lakes,
    IReadOnlyList<Fence> Fences);
