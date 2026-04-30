using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for <see cref="GameRepository"/> using an EF Core InMemory database.
/// Each test gets its own isolated database to avoid state leakage.
/// </summary>
public sealed class GameRepositoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DbContextOptions<ScrambleCoinDbContext> BuildOptions(string dbName) =>
        new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

    /// <summary>
    /// Reads a private instance field from a domain object via reflection.
    /// Used to verify that internal tracking state (e.g. <c>_movedPieceIds</c>,
    /// <c>_placePhaseDone</c>) survives a persistence round-trip.
    /// </summary>
    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"Field '{fieldName}' not found on '{instance.GetType().Name}'.");
        return (T)field.GetValue(instance)!;
    }

    /// <summary>
    /// Creates a minimal 5-piece lineup for the given player using simple pieces
    /// with no special constraints.
    /// </summary>
    private static Lineup BuildLineup(Guid playerId) =>
        new(Enumerable.Range(0, 5).Select(i =>
            new Piece(
                id: Guid.NewGuid(),
                name: $"Piece{i}",
                playerId: playerId,
                entryPointType: EntryPointType.Anywhere,
                movementType: MovementType.Orthogonal,
                maxDistance: 3,
                movesPerTurn: 1)));

    /// <summary>
    /// Creates a new <see cref="Game"/> in <see cref="GameStatus.WaitingForBots"/> with two
    /// distinct player IDs and a fresh board.
    /// </summary>
    private static Game BuildWaitingGame(out Guid playerOne, out Guid playerTwo)
    {
        playerOne = Guid.NewGuid();
        playerTwo = Guid.NewGuid();
        var board = new Board();
        return new Game(Guid.NewGuid(), playerOne, playerTwo, board);
    }

    // ── Round-trip: WaitingForBots state ──────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsMatchingId()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsMatchingId));
        var game = BuildWaitingGame(out _, out _);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        var repo = new GameRepository(writeCtx);
        await repo.SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var readRepo = new GameRepository(readCtx);
        var loaded = await readRepo.GetByIdAsync(game.Id);

        Assert.Equal(game.Id, loaded.Id);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsMatchingPlayerOne()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsMatchingPlayerOne));
        var game = BuildWaitingGame(out var playerOne, out _);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(playerOne, loaded.PlayerOne);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsMatchingPlayerTwo()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsMatchingPlayerTwo));
        var game = BuildWaitingGame(out _, out var playerTwo);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(playerTwo, loaded.PlayerTwo);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsCorrectStatus()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsCorrectStatus));
        var game = BuildWaitingGame(out _, out _);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(GameStatus.WaitingForBots, loaded.Status);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsZeroTurnNumber()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_WaitingForBots_ReturnsZeroTurnNumber));
        var game = BuildWaitingGame(out _, out _);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(0, loaded.TurnNumber);
    }

    // ── Round-trip: InProgress state (with lineups, board pieces, scores) ─────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_InProgressGame_ReturnsInProgressStatus()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_InProgressGame_ReturnsInProgressStatus));
        var (game, _, _) = BuildStartedGame();

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(GameStatus.InProgress, loaded.Status);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_InProgressGame_ReturnsTurnNumberOne()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_InProgressGame_ReturnsTurnNumberOne));
        var (game, _, _) = BuildStartedGame();

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(1, loaded.TurnNumber);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_InProgressGame_LineupPlayerOneIsNotNull()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_InProgressGame_LineupPlayerOneIsNotNull));
        var (game, _, _) = BuildStartedGame();

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.NotNull(loaded.LineupPlayerOne);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_InProgressGame_LineupPlayerTwoIsNotNull()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_InProgressGame_LineupPlayerTwoIsNotNull));
        var (game, _, _) = BuildStartedGame();

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.NotNull(loaded.LineupPlayerTwo);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_InProgressGame_LineupPlayerOnePreservesPieceCount()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_InProgressGame_LineupPlayerOnePreservesPieceCount));
        var (game, _, _) = BuildStartedGame();

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(Lineup.RequiredPieceCount, loaded.LineupPlayerOne!.Pieces.Count);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_InProgressGame_LineupPlayerTwoPreservesPieceCount()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_InProgressGame_LineupPlayerTwoPreservesPieceCount));
        var (game, _, _) = BuildStartedGame();

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(Lineup.RequiredPieceCount, loaded.LineupPlayerTwo!.Pieces.Count);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_InProgressGame_ScoresArePreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_InProgressGame_ScoresArePreserved));
        var (game, playerOne, playerTwo) = BuildStartedGame();

        // Advance past CoinSpawn so we can add scores (game must be InProgress — it is).
        game.AddScore(playerOne, 5);
        game.AddScore(playerTwo, 3);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(5, loaded.Scores[playerOne]);
        Assert.Equal(3, loaded.Scores[playerTwo]);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_InProgressGame_CurrentPhaseIsPreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_InProgressGame_CurrentPhaseIsPreserved));
        var (game, _, _) = BuildStartedGame();
        // After Start(), CurrentPhase == CoinSpawn
        Assert.Equal(TurnPhase.CoinSpawn, game.CurrentPhase);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(TurnPhase.CoinSpawn, loaded.CurrentPhase);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_MovePhase_MovePhaseActivePlayerIsPreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_MovePhase_MovePhaseActivePlayerIsPreserved));
        var (game, playerOne, _) = BuildStartedGame();

        // Advance through CoinSpawn → PlacePhase → MovePhase
        game.AdvancePhase(); // → PlacePhase
        game.AdvancePhase(); // → MovePhase (MovePhaseActivePlayer = PlayerOne)

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(playerOne, loaded.MovePhaseActivePlayer);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_MovePhase_CurrentPhaseIsMovePhase()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_MovePhase_CurrentPhaseIsMovePhase));
        var (game, _, _) = BuildStartedGame();

        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.AdvancePhase(); // PlacePhase → MovePhase

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(TurnPhase.MovePhase, loaded.CurrentPhase);
    }

    // ── Round-trip: board pieces survive reload ────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PieceOnBoard_PiecePositionIsPreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PieceOnBoard_PiecePositionIsPreserved));
        var (game, playerOne, _) = BuildStartedGame();

        // Place the first piece from PlayerOne's lineup on tile (2, 3)
        var piece = game.LineupPlayerOne!.Pieces[0];
        var targetPosition = new Position(2, 3);
        piece.PlaceAt(targetPosition);
        game.Board.GetTile(targetPosition).SetOccupant(piece);
        game.AddPieceToBoard(playerOne);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        var loadedPiece = loaded.LineupPlayerOne!.Pieces.Single(p => p.Id == piece.Id);
        Assert.Equal(2, loadedPiece.Position!.Row);
        Assert.Equal(3, loadedPiece.Position.Col);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PieceOnBoard_BoardTileShowsPiece()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PieceOnBoard_BoardTileShowsPiece));
        var (game, playerOne, _) = BuildStartedGame();

        var piece = game.LineupPlayerOne!.Pieces[0];
        var targetPosition = new Position(4, 5);
        piece.PlaceAt(targetPosition);
        game.Board.GetTile(targetPosition).SetOccupant(piece);
        game.AddPieceToBoard(playerOne);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        var tile = loaded.Board.GetTile(targetPosition);
        Assert.NotNull(tile.AsPiece);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PiecesOnBoard_CounterIsPreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PiecesOnBoard_CounterIsPreserved));
        var (game, playerOne, _) = BuildStartedGame();

        var piece = game.LineupPlayerOne!.Pieces[0];
        piece.PlaceAt(new Position(1, 1));
        game.Board.GetTile(new Position(1, 1)).SetOccupant(piece);
        game.AddPieceToBoard(playerOne);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Equal(1, loaded.PiecesOnBoard[playerOne]);
    }

    // ── GetByIdAsync: unknown ID ──────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_UnknownId_ThrowsInvalidOperationException()
    {
        var options = BuildOptions(nameof(GetByIdAsync_UnknownId_ThrowsInvalidOperationException));
        await using var ctx = new ScrambleCoinDbContext(options);
        var repo = new GameRepository(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ExceptionMessageContainsId()
    {
        var options = BuildOptions(nameof(GetByIdAsync_UnknownId_ExceptionMessageContainsId));
        await using var ctx = new ScrambleCoinDbContext(options);
        var repo = new GameRepository(ctx);

        var unknownId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.GetByIdAsync(unknownId));

        Assert.Contains(unknownId.ToString(), ex.Message);
    }

    // ── Domain events are NOT persisted ──────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ClearsDomainEventsAfterPersistence()
    {
        var options = BuildOptions(nameof(SaveAsync_ClearsDomainEventsAfterPersistence));
        var (game, _, _) = BuildStartedGame();

        // Start() raised a GameStarted domain event.
        Assert.NotEmpty(game.DomainEvents);

        await using var ctx = new ScrambleCoinDbContext(options);
        await new GameRepository(ctx).SaveAsync(game);

        Assert.Empty(game.DomainEvents);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_LoadedGameHasNoDomainEvents()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_LoadedGameHasNoDomainEvents));
        var (game, _, _) = BuildStartedGame();

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.Empty(loaded.DomainEvents);
    }

    [Fact]
    public async Task SaveAsync_WaitingForBotsGame_ClearsDomainEvents()
    {
        var options = BuildOptions(nameof(SaveAsync_WaitingForBotsGame_ClearsDomainEvents));
        var game = BuildWaitingGame(out _, out _);
        // WaitingForBots game raises no domain events in its constructor, but verify
        // the post-save clear still works correctly (events remain empty).
        Assert.Empty(game.DomainEvents);

        await using var ctx = new ScrambleCoinDbContext(options);
        await new GameRepository(ctx).SaveAsync(game);

        Assert.Empty(game.DomainEvents);
    }

    // ── Update (second SaveAsync call) ────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_CalledTwice_UpdatesExistingRecord()
    {
        var options = BuildOptions(nameof(SaveAsync_CalledTwice_UpdatesExistingRecord));
        var (game, playerOne, _) = BuildStartedGame();

        await using var ctx = new ScrambleCoinDbContext(options);
        var repo = new GameRepository(ctx);

        // First save
        await repo.SaveAsync(game);

        // Mutate and save again
        game.AddScore(playerOne, 10);
        await repo.SaveAsync(game);

        var loaded = await repo.GetByIdAsync(game.Id);
        Assert.Equal(10, loaded.Scores[playerOne]);
    }

    [Fact]
    public async Task SaveAsync_CalledTwice_DoesNotDuplicateRecord()
    {
        var options = BuildOptions(nameof(SaveAsync_CalledTwice_DoesNotDuplicateRecord));
        var (game, _, _) = BuildStartedGame();

        await using var ctx = new ScrambleCoinDbContext(options);
        var repo = new GameRepository(ctx);

        await repo.SaveAsync(game);
        await repo.SaveAsync(game);

        // If we can GetById successfully without ambiguity it means exactly one record exists.
        var loaded = await repo.GetByIdAsync(game.Id);
        Assert.Equal(game.Id, loaded.Id);
    }

    // ── _movedPieceIds survives restart mid-MovePhase ─────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_MovedPieceIds_ArePersisted()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_MovedPieceIds_ArePersisted));
        var (game, playerOne, _) = BuildStartedGame();

        // Advance to MovePhase
        game.AdvancePhase(); // → PlacePhase
        game.AdvancePhase(); // → MovePhase

        // Simulate a piece having moved by adding its ID directly to the private set
        // (avoids the complexity of building a full valid move path).
        var piece = game.LineupPlayerOne!.Pieces[0];
        var movedPieceIds = GetPrivateField<HashSet<Guid>>(game, "_movedPieceIds");
        movedPieceIds.Add(piece.Id);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        // After reload the piece's ID must still appear in _movedPieceIds.
        var loadedMovedPieceIds = GetPrivateField<HashSet<Guid>>(loaded, "_movedPieceIds");
        Assert.Contains(piece.Id, loadedMovedPieceIds);
    }

    // ── PlacePhase tracking survives restart ──────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PlacePhaseDone_IsPreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PlacePhaseDone_IsPreserved));
        var (game, playerOne, _) = BuildStartedGame();

        // Advance to PlacePhase
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Mark playerOne as done with the place phase using SkipPlacement.
        game.SkipPlacement(playerOne);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        // After reload, the _placePhaseDone set must still contain playerOne.
        var loadedPlacePhaseDone = GetPrivateField<HashSet<Guid>>(loaded, "_placePhaseDone");
        Assert.Contains(playerOne, loadedPlacePhaseDone);
    }

    // ── Board obstacles survive round-trip ────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_RockObstacle_IsPreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_RockObstacle_IsPreserved));
        var playerOne = Guid.NewGuid();
        var playerTwo = Guid.NewGuid();
        var board = new Board();
        board.AddRock(new Domain.Obstacles.Rock(new Position(3, 3)));
        var game = new Game(Guid.NewGuid(), playerOne, playerTwo, board);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        var obstacles = loaded.Board.GetAllObstacles();
        Assert.Single(obstacles.Rocks);
        Assert.Equal(3, obstacles.Rocks[0].Position.Row);
        Assert.Equal(3, obstacles.Rocks[0].Position.Col);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_LakeObstacle_IsPreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_LakeObstacle_IsPreserved));
        var playerOne = Guid.NewGuid();
        var playerTwo = Guid.NewGuid();
        var board = new Board();
        board.AddLake(new Domain.Obstacles.Lake(new Position(2, 2)));
        var game = new Game(Guid.NewGuid(), playerOne, playerTwo, board);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        var obstacles = loaded.Board.GetAllObstacles();
        Assert.Single(obstacles.Lakes);
        Assert.Equal(2, obstacles.Lakes[0].TopLeft.Row);
        Assert.Equal(2, obstacles.Lakes[0].TopLeft.Col);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_FenceObstacle_IsPreserved()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_FenceObstacle_IsPreserved));
        var playerOne = Guid.NewGuid();
        var playerTwo = Guid.NewGuid();
        var board = new Board();
        board.AddFence(new Domain.Obstacles.Fence(new Position(3, 3), new Position(3, 4)));
        var game = new Game(Guid.NewGuid(), playerOne, playerTwo, board);

        await using var writeCtx = new ScrambleCoinDbContext(options);
        await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        var obstacles = loaded.Board.GetAllObstacles();
        Assert.Single(obstacles.Fences);
        Assert.Equal(3, obstacles.Fences[0].From.Row);
        Assert.Equal(3, obstacles.Fences[0].From.Col);
        Assert.Equal(3, obstacles.Fences[0].To.Row);
        Assert.Equal(4, obstacles.Fences[0].To.Col);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="Game"/> that has been started (both lineups set, <see cref="Game.Start"/> called).
    /// Returns the game and both player IDs.
    /// </summary>
    private static (Game game, Guid playerOne, Guid playerTwo) BuildStartedGame()
    {
        var playerOne = Guid.NewGuid();
        var playerTwo = Guid.NewGuid();
        var board = new Board();
        var game = new Game(Guid.NewGuid(), playerOne, playerTwo, board);

        game.SetLineup(playerOne, BuildLineup(playerOne));
        game.SetLineup(playerTwo, BuildLineup(playerTwo));
        game.Start();

        return (game, playerOne, playerTwo);
    }
}
