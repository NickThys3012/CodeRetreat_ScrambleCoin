using System.Reflection;
using System.Text.Json;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Infrastructure.Persistence.Records;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed implementation of <see cref="IGameRepository"/>.
/// Persists the <see cref="Game"/> aggregate by mapping its state to a <see cref="GameRecord"/>
/// POCO (scalar columns + JSON columns for complex structures) and reconstructing
/// the domain object on load via reflection where private setters/fields are involved.
/// </summary>
public sealed class GameRepository : IGameRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ScrambleCoinDbContext _context;

    public GameRepository(ScrambleCoinDbContext context)
    {
        _context = context;
    }

    // ── IGameRepository ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Game> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.Games.FindAsync([id], cancellationToken)
            ?? throw new GameNotFoundException(id);

        return ReconstructGame(record);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Game game, CancellationToken cancellationToken = default)
    {
        var record = ExtractRecord(game);

        var existing = await _context.Games.FindAsync([game.Id], cancellationToken);
        if (existing is null)
        {
            _context.Games.Add(record);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(record);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Clear domain events after successful persistence (they are transient).
        game.ClearDomainEvents();
    }

    // ── Extraction (Game → GameRecord) ────────────────────────────────────────

    private static GameRecord ExtractRecord(Game game)
    {
        // Access private fields via reflection.
        var type = typeof(Game);
        var placePhaseDone = GetPrivateField<HashSet<Guid>>(type, "_placePhaseDone", game);
        var movedPieceIds = GetPrivateField<HashSet<Guid>>(type, "_movedPieceIds", game);

        return new GameRecord
        {
            Id = game.Id,
            PlayerOne = game.PlayerOne,
            PlayerTwo = game.PlayerTwo,
            Status = (int)game.Status,
            TurnNumber = game.TurnNumber,
            CurrentPhase = game.CurrentPhase.HasValue ? (int)game.CurrentPhase.Value : null,
            MovePhaseActivePlayer = game.MovePhaseActivePlayer,
            ScoresJson = SerializeGuidIntDictionary(game.Scores),
            PiecesOnBoardJson = SerializeGuidIntDictionary(game.PiecesOnBoard),
            PlacePhaseDoneJson = SerializeGuidSet(placePhaseDone),
            MovedPieceIdsJson = SerializeGuidSet(movedPieceIds),
            LineupPlayerOneJson = game.LineupPlayerOne is not null
                ? SerializeLineup(game.LineupPlayerOne)
                : null,
            LineupPlayerTwoJson = game.LineupPlayerTwo is not null
                ? SerializeLineup(game.LineupPlayerTwo)
                : null,
            BoardStateJson = SerializeBoardState(game.Board)
        };
    }

    // ── Reconstruction (GameRecord → Game) ────────────────────────────────────

    private static Game ReconstructGame(GameRecord record)
    {
        // 1. Deserialize pieces for both lineups (with their current positions).
        var pieceDtosOne = record.LineupPlayerOneJson is not null
            ? JsonSerializer.Deserialize<List<PieceDto>>(record.LineupPlayerOneJson, JsonOptions) ?? []
            : new List<PieceDto>();

        var pieceDtosTwo = record.LineupPlayerTwoJson is not null
            ? JsonSerializer.Deserialize<List<PieceDto>>(record.LineupPlayerTwoJson, JsonOptions) ?? []
            : new List<PieceDto>();

        // 2. Materialise Piece domain objects (Position is applied below).
        var piecesOne = pieceDtosOne.Select(ReconstructPiece).ToList();
        var piecesTwo = pieceDtosTwo.Select(ReconstructPiece).ToList();

        var allPiecesById = piecesOne
            .Concat(piecesTwo)
            .ToDictionary(p => p.Id);

        // 3. Reconstruct Board with obstacles.
        var board = new Board();
        var boardState = JsonSerializer.Deserialize<BoardStateDto>(record.BoardStateJson, JsonOptions);

        if (boardState is not null)
        {
            foreach (var r in boardState.Rocks)
                board.AddRock(new Rock(new Position(r.Row, r.Col)));

            foreach (var l in boardState.Lakes)
                board.AddLake(new Lake(new Position(l.TopLeftRow, l.TopLeftCol)));

            foreach (var f in boardState.Fences)
                board.AddFence(new Fence(
                    new Position(f.FromRow, f.FromCol),
                    new Position(f.ToRow, f.ToCol)));

            // 4. Place occupants on tiles.
            foreach (var occ in boardState.Occupants)
            {
                var tile = board.GetTile(new Position(occ.Row, occ.Col));
                if (occ.IsCoin && occ.CoinType.HasValue)
                {
                    tile.SetOccupant(new Coin((CoinType)occ.CoinType.Value));
                }
                else if (!occ.IsCoin && occ.PieceId.HasValue &&
                         allPiecesById.TryGetValue(occ.PieceId.Value, out var piece))
                {
                    // Restore piece's board position.
                    piece.PlaceAt(new Position(occ.Row, occ.Col));
                    tile.SetOccupant(piece);
                }
            }
        }

        // 5. Create Game via the standard constructor.
        //    The constructor initialises _scores and _piecesOnBoard to zeroes; we override them below.
        var game = new Game(record.Id, record.PlayerOne, record.PlayerTwo, board);

        var gameType = typeof(Game);

        // 6. Restore scalar private-setter properties via reflection.
        SetPrivateProperty(gameType, nameof(Game.Status), game, (GameStatus)record.Status);
        SetPrivateProperty(gameType, nameof(Game.TurnNumber), game, record.TurnNumber);
        SetPrivateProperty(gameType, nameof(Game.CurrentPhase), game,
            record.CurrentPhase.HasValue ? (TurnPhase?)((TurnPhase)record.CurrentPhase.Value) : null);
        SetPrivateProperty(gameType, nameof(Game.MovePhaseActivePlayer), game, record.MovePhaseActivePlayer);

        // 7. Restore Lineups (bypasses the WaitingForBots guard in SetLineup).
        if (piecesOne.Count > 0)
        {
            var lineup = new Lineup(piecesOne);
            SetPrivateProperty(gameType, nameof(Game.LineupPlayerOne), game, lineup);
        }

        if (piecesTwo.Count > 0)
        {
            var lineup = new Lineup(piecesTwo);
            SetPrivateProperty(gameType, nameof(Game.LineupPlayerTwo), game, lineup);
        }

        // 8. Restore private Dictionary fields in-place (they are readonly; modify contents).
        var scores = DeserializeGuidIntDictionary(record.ScoresJson);
        UpdateInPlaceDictionary(GetPrivateField<Dictionary<Guid, int>>(gameType, "_scores", game), scores);

        var piecesOnBoard = DeserializeGuidIntDictionary(record.PiecesOnBoardJson);
        UpdateInPlaceDictionary(GetPrivateField<Dictionary<Guid, int>>(gameType, "_piecesOnBoard", game), piecesOnBoard);

        // 9. Restore private HashSet fields in-place.
        var placePhaseDone = DeserializeGuidSet(record.PlacePhaseDoneJson);
        UpdateInPlaceSet(GetPrivateField<HashSet<Guid>>(gameType, "_placePhaseDone", game), placePhaseDone);

        var movedPieceIds = DeserializeGuidSet(record.MovedPieceIdsJson);
        UpdateInPlaceSet(GetPrivateField<HashSet<Guid>>(gameType, "_movedPieceIds", game), movedPieceIds);

        // 10. Clear any spurious domain events that the Game constructor might have raised.
        //     (Currently it raises none, but this future-proofs reconstruction.)
        game.ClearDomainEvents();

        return game;
    }

    // ── Serialisation helpers ─────────────────────────────────────────────────

    private static string SerializeLineup(Lineup lineup)
    {
        var dtos = lineup.Pieces.Select(p => new PieceDto(
            Id: p.Id,
            Name: p.Name,
            PlayerId: p.PlayerId,
            EntryPointType: (int)p.EntryPointType,
            MovementType: (int)p.MovementType,
            MaxDistance: p.MaxDistance,
            MovesPerTurn: p.MovesPerTurn,
            PositionRow: p.Position?.Row,
            PositionCol: p.Position?.Col)).ToList();

        return JsonSerializer.Serialize(dtos, JsonOptions);
    }

    private static string SerializeBoardState(Board board)
    {
        var obstacles = board.GetAllObstacles();

        var rocks = obstacles.Rocks
            .Select(r => new RockDto(r.Position.Row, r.Position.Col))
            .ToList();

        var lakes = obstacles.Lakes
            .Select(l => new LakeDto(l.TopLeft.Row, l.TopLeft.Col))
            .ToList();

        var fences = obstacles.Fences
            .Select(f => new FenceDto(f.From.Row, f.From.Col, f.To.Row, f.To.Col))
            .ToList();

        var occupants = board.GetAllOccupiedTiles()
            .Select(tile =>
            {
                if (tile.AsCoin is { } coin)
                    return new TileOccupantDto(tile.Position.Row, tile.Position.Col, IsCoin: true, (int)coin.CoinType, PieceId: null);

                if (tile.AsPiece is { } piece)
                    return new TileOccupantDto(tile.Position.Row, tile.Position.Col, IsCoin: false, CoinType: null, piece.Id);

                return null;
            })
            .Where(o => o is not null)
            .Cast<TileOccupantDto>()
            .ToList();

        var dto = new BoardStateDto(rocks, lakes, fences, occupants);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static string SerializeGuidIntDictionary(IReadOnlyDictionary<Guid, int> dict)
    {
        // Convert Guid keys to strings for JSON serialisation.
        var strDict = dict.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        return JsonSerializer.Serialize(strDict, JsonOptions);
    }

    private static string SerializeGuidSet(HashSet<Guid> set)
    {
        var strList = set.Select(g => g.ToString()).ToList();
        return JsonSerializer.Serialize(strList, JsonOptions);
    }

    private static Dictionary<Guid, int> DeserializeGuidIntDictionary(string json)
    {
        var strDict = JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions) ?? [];
        return strDict.ToDictionary(kv => Guid.Parse(kv.Key), kv => kv.Value);
    }

    private static HashSet<Guid> DeserializeGuidSet(string json)
    {
        var strList = JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        return [.. strList.Select(Guid.Parse)];
    }

    // ── Domain-object construction helpers ────────────────────────────────────

    private static Piece ReconstructPiece(PieceDto dto)
    {
        return new Piece(
            id: dto.Id,
            name: dto.Name,
            playerId: dto.PlayerId,
            entryPointType: (EntryPointType)dto.EntryPointType,
            movementType: (MovementType)dto.MovementType,
            maxDistance: dto.MaxDistance,
            movesPerTurn: dto.MovesPerTurn);
    }

    // ── Reflection helpers ────────────────────────────────────────────────────

    private static T GetPrivateField<T>(Type type, string fieldName, object instance)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"Private field '{fieldName}' not found on type '{type.Name}'.");
        return (T)field.GetValue(instance)!;
    }

    private static void SetPrivateProperty(Type type, string propertyName, object instance, object? value)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"Property '{propertyName}' not found on type '{type.Name}'.");
        prop.SetValue(instance, value);
    }

    private static void UpdateInPlaceDictionary(Dictionary<Guid, int> target, Dictionary<Guid, int> source)
    {
        target.Clear();
        foreach (var kv in source)
            target[kv.Key] = kv.Value;
    }

    private static void UpdateInPlaceSet(HashSet<Guid> target, HashSet<Guid> source)
    {
        target.Clear();
        foreach (var id in source)
            target.Add(id);
    }
}
