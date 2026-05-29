using System.Collections.ObjectModel;
using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.GetBoardState;

/// <summary>
/// Handles <see cref="GetSpectatorBoardStateQuery"/>:
/// loads the game and maps domain state to <see cref="BoardStateDto"/> from
/// PlayerOne's perspective — no bot-token validation required.
/// Intended for internal use by the SignalR broadcaster to push live updates to spectators.
/// </summary>
public sealed class GetSpectatorBoardStateQueryHandler : IRequestHandler<GetSpectatorBoardStateQuery, BoardStateDto>
{
    private readonly IGameRepository _gameRepository;

    public GetSpectatorBoardStateQueryHandler(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    public async Task<BoardStateDto> Handle(GetSpectatorBoardStateQuery request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        var playerOneId = game.PlayerOne;
        var playerTwoId = game.PlayerTwo;

        var phase = game.CurrentPhase?.ToString();

        var playerOnePieces = GetPiecesForPlayer(game, playerOneId);
        var playerTwoPieces = GetPiecesForPlayer(game, playerTwoId);

        var obstacles = game.Board.GetAllObstacles();
        var tiles = BuildTiles(game.Board, obstacles);
        var availableCoins = BuildAvailableCoins(game.Board);

        game.Scores.TryGetValue(playerOneId, out var playerOneScore);
        game.Scores.TryGetValue(playerTwoId, out var playerTwoScore);

        var activePlayer = game.MovePhaseActivePlayer?.ToString();

        return new BoardStateDto(
            Turn: game.TurnNumber,
            Phase: phase,
            YourScore: playerOneScore,
            OpponentScore: playerTwoScore,
            Board: new BoardDto(tiles),
            YourPieces: playerOnePieces,
            OpponentPieces: playerTwoPieces,
            AvailableCoins: availableCoins,
            ActivePlayer: activePlayer);
    }

    // ── Private mapping helpers ───────────────────────────────────────────────

    private static ReadOnlyCollection<PieceDto> GetPiecesForPlayer(Game game, Guid playerId)
    {
        var lineup = playerId == game.PlayerOne ? game.LineupPlayerOne : game.LineupPlayerTwo;
        return lineup is null ?
            Array.Empty<PieceDto>().AsReadOnly() :
            lineup.Pieces.Select(MapPiece).ToList().AsReadOnly();
    }

    private static PieceDto MapPiece(Piece piece) =>
        new(
            PieceId: piece.Id,
            Name: piece.Name,
            Position: piece.Position is not null ? new PositionDto(piece.Position.Row, piece.Position.Col) : null,
            MovementType: piece.MovementType.ToString(),
            MaxDistance: piece.MaxDistance,
            MovesPerTurn: piece.MovesPerTurn,
            IsOnBoard: piece.IsOnBoard);

    private static ReadOnlyCollection<TileDto> BuildTiles(Board board, BoardObstacles obstacles)
    {
        var tiles = new List<TileDto>(Board.Size * Board.Size);

        for (var row = 0; row < Board.Size; row++)
        for (var col = 0; col < Board.Size; col++)
        {
            var position = new Position(row, col);
            var tile = board.GetTile(position);
            var isObstacle = board.IsObstacleCovering(position);
            var occupant = MapOccupant(tile);
            var fencedEdges = GetFencedEdges(position, obstacles.Fences);

            tiles.Add(new TileDto(
                Position: new PositionDto(row, col),
                IsObstacle: isObstacle,
                Occupant: occupant,
                FencedEdges: fencedEdges));
        }

        return tiles.AsReadOnly();
    }

    private static TileOccupantDto? MapOccupant(Tile tile)
    {
        if (tile.AsCoin is { } coin)
            return new TileOccupantDto(
                Type: "coin",
                CoinType: coin.CoinType.ToString(),
                Value: coin.Value);

        if (tile.AsPiece is { } piece)
            return new TileOccupantDto(
                Type: "piece",
                PieceId: piece.Id,
                Name: piece.Name,
                PlayerId: piece.PlayerId);

        return null;
    }

    private static ReadOnlyCollection<string> GetFencedEdges(Position position, IReadOnlyList<Fence> fences)
    {
        var edges = new List<string>(4);

        if (position.Row > 0)
        {
            var north = new Position(position.Row - 1, position.Col);
            if (fences.Any(f => f.IsOnEdge(position, north)))
                edges.Add("North");
        }

        if (position.Row < Board.Size - 1)
        {
            var south = new Position(position.Row + 1, position.Col);
            if (fences.Any(f => f.IsOnEdge(position, south)))
                edges.Add("South");
        }

        if (position.Col > 0)
        {
            var west = new Position(position.Row, position.Col - 1);
            if (fences.Any(f => f.IsOnEdge(position, west)))
                edges.Add("West");
        }

        if (position.Col >= Board.Size - 1)
        {
            return edges.AsReadOnly();
        }
        
        var east = new Position(position.Row, position.Col + 1);
        if (fences.Any(f => f.IsOnEdge(position, east)))
            edges.Add("East");
        
        return edges.AsReadOnly();
    }

    private static ReadOnlyCollection<CoinDto> BuildAvailableCoins(Board board)
    {
        return board.GetAllCoins()
            .Select(tile => new CoinDto(
                Position: new PositionDto(tile.Position.Row, tile.Position.Col),
                CoinType: tile.AsCoin!.CoinType.ToString(),
                Value: tile.AsCoin!.Value))
            .ToList()
            .AsReadOnly();
    }
}
