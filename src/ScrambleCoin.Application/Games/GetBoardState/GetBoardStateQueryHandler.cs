using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.GetBoardState;

/// <summary>
/// Handles <see cref="GetBoardStateQuery"/>:
/// <list type="bullet">
///   <item>Validates the bot token against <see cref="IBotRegistrationRepository"/>.</item>
///   <item>Loads the game via <see cref="IGameRepository"/>.</item>
///   <item>Maps domain state to <see cref="BoardStateDto"/> (caller-relative: yourScore, yourPieces, etc.).</item>
/// </list>
/// </summary>
public sealed class GetBoardStateQueryHandler : IRequestHandler<GetBoardStateQuery, BoardStateDto>
{
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly ILogger<GetBoardStateQueryHandler> _logger;

    public GetBoardStateQueryHandler(
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        ILogger<GetBoardStateQueryHandler> logger)
    {
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _logger = logger;
    }

    public async Task<BoardStateDto> Handle(GetBoardStateQuery request, CancellationToken cancellationToken)
    {
        // 1. Validate bot token
        var registration = await _botRegistrationRepository.GetByTokenAsync(request.BotToken, cancellationToken);

        if (registration is null || registration.GameId != request.GameId)
            throw new UnauthorizedGameAccessException();

        // 2. Load game (throws GameNotFoundException if missing)
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        var callerId = registration.PlayerId;
        var opponentId = game.PlayerOne == callerId ? game.PlayerTwo : game.PlayerOne;

        var phase = game.CurrentPhase?.ToString();

        _logger.LogInformation(
            "Board state queried: GameId={GameId} BotId={BotId} Turn={Turn} Phase={Phase}",
            game.Id, callerId, game.TurnNumber, phase);

        // 3. Gather all pieces from both lineups
        var callerPieces = GetPiecesForPlayer(game, callerId);
        var opponentPieces = GetPiecesForPlayer(game, opponentId);

        // 4. Build board DTOs
        var obstacles = game.Board.GetAllObstacles();
        var tiles = BuildTiles(game.Board, obstacles);
        var availableCoins = BuildAvailableCoins(game.Board);

        // 5. Scores
        game.Scores.TryGetValue(callerId, out var yourScore);
        game.Scores.TryGetValue(opponentId, out var opponentScore);

        // 6. Active player
        var activePlayer = game.MovePhaseActivePlayer?.ToString();

        return new BoardStateDto(
            Turn: game.TurnNumber,
            Phase: phase,
            YourScore: yourScore,
            OpponentScore: opponentScore,
            Board: new BoardDto(tiles),
            YourPieces: callerPieces,
            OpponentPieces: opponentPieces,
            AvailableCoins: availableCoins,
            ActivePlayer: activePlayer);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<PieceDto> GetPiecesForPlayer(Game game, Guid playerId)
    {
        var lineup = playerId == game.PlayerOne ? game.LineupPlayerOne : game.LineupPlayerTwo;
        if (lineup is null)
            return Array.Empty<PieceDto>();

        return lineup.Pieces.Select(MapPiece).ToList().AsReadOnly();
    }

    private static PieceDto MapPiece(Piece piece) =>
        new PieceDto(
            PieceId: piece.Id,
            Name: piece.Name,
            Position: piece.Position is not null ? new PositionDto(piece.Position.Row, piece.Position.Col) : null,
            MovementType: piece.MovementType.ToString(),
            MaxDistance: piece.MaxDistance,
            MovesPerTurn: piece.MovesPerTurn,
            IsOnBoard: piece.IsOnBoard);

    private static IReadOnlyList<TileDto> BuildTiles(Board board, Domain.Entities.BoardObstacles obstacles)
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

    private static IReadOnlyList<string> GetFencedEdges(Position position, IReadOnlyList<Fence> fences)
    {
        var edges = new List<string>(4);

        // North: (row-1, col)
        if (position.Row > 0)
        {
            var north = new Position(position.Row - 1, position.Col);
            if (fences.Any(f => f.IsOnEdge(position, north)))
                edges.Add("North");
        }

        // South: (row+1, col)
        if (position.Row < Board.Size - 1)
        {
            var south = new Position(position.Row + 1, position.Col);
            if (fences.Any(f => f.IsOnEdge(position, south)))
                edges.Add("South");
        }

        // West: (row, col-1)
        if (position.Col > 0)
        {
            var west = new Position(position.Row, position.Col - 1);
            if (fences.Any(f => f.IsOnEdge(position, west)))
                edges.Add("West");
        }

        // East: (row, col+1)
        if (position.Col < Board.Size - 1)
        {
            var east = new Position(position.Row, position.Col + 1);
            if (fences.Any(f => f.IsOnEdge(position, east)))
                edges.Add("East");
        }

        return edges.AsReadOnly();
    }

    private static IReadOnlyList<CoinDto> BuildAvailableCoins(Board board)
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
