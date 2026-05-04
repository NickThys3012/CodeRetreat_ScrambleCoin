using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.GetBoardState;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="GetBoardStateQueryHandler"/> (Issue #38).
/// </summary>
public class GetBoardStateQueryHandlerTests
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a started game (CoinSpawn phase) with both lineups set.</summary>
    private static (Game game, Guid p1, Guid p2) NewStartedGame()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var pieces1 = DefaultLineup
            .Select(n => new Piece(Guid.NewGuid(), n, p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var pieces2 = DefaultLineup
            .Select(n => new Piece(Guid.NewGuid(), n, p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start(); // → TurnNumber = 1, Phase = CoinSpawn

        return (game, p1, p2);
    }

    /// <summary>Creates a bot registration for the given player and game.</summary>
    private static DomainBotReg MakeRegistration(Guid gameId, Guid playerId)
        => new(Guid.NewGuid(), playerId, gameId);

    private static GetBoardStateQueryHandler BuildHandler(
        IGameRepository gameRepo,
        IBotRegistrationRepository botRegRepo)
    {
        var logger = Substitute.For<ILogger<GetBoardStateQueryHandler>>();
        return new GetBoardStateQueryHandler(gameRepo, botRegRepo, logger);
    }

    // ── AC 1 / AC 2 : Returns a well-formed BoardStateDto ─────────────────────

    [Fact]
    public async Task Handle_ValidRequest_ReturnsBoardStateDto()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.NotNull(dto);
    }

    [Fact]
    public async Task Handle_ValidRequest_TurnNumberMatchesGameTurnNumber()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: Turn 1 after Start()
        Assert.Equal(1, dto.Turn);
    }

    [Fact]
    public async Task Handle_ValidRequest_PhaseMatchesGameCurrentPhase()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: CoinSpawn is the first phase after Start
        Assert.Equal("CoinSpawn", dto.Phase);
    }

    [Fact]
    public async Task Handle_ValidRequest_YourScoreStartsAtZero()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.Equal(0, dto.YourScore);
    }

    [Fact]
    public async Task Handle_ValidRequest_OpponentScoreStartsAtZero()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.Equal(0, dto.OpponentScore);
    }

    // ── AC 3 : Board contains 64 tiles ────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidRequest_BoardContains64Tiles()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: 8×8 = 64
        Assert.Equal(64, dto.Board.Tiles.Count);
    }

    [Fact]
    public async Task Handle_ValidRequest_EachTileHasAPosition()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: every tile has a non-null position
        Assert.All(dto.Board.Tiles, tile => Assert.NotNull(tile.Position));
    }

    [Fact]
    public async Task Handle_ValidRequest_TilePositionsCoverFullBoard()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: rows 0–7, cols 0–7 are all present
        var positions = dto.Board.Tiles.Select(t => (t.Position.Row, t.Position.Col)).ToHashSet();
        for (var row = 0; row < 8; row++)
        for (var col = 0; col < 8; col++)
            Assert.Contains((row, col), positions);
    }

    [Fact]
    public async Task Handle_ValidRequest_EachTileHasFencedEdgesCollection()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: every tile has a non-null FencedEdges list (may be empty)
        Assert.All(dto.Board.Tiles, tile => Assert.NotNull(tile.FencedEdges));
    }

    [Fact]
    public async Task Handle_EmptyBoard_NoTilesAreObstacles()
    {
        // Arrange: plain board with no obstacles
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.All(dto.Board.Tiles, tile => Assert.False(tile.IsObstacle));
    }

    // ── AC 4 : Pieces have correct shape ──────────────────────────────────────

    [Fact]
    public async Task Handle_ValidRequest_YourPiecesContainsCallerLineup()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: 5 pieces per lineup
        Assert.Equal(DefaultLineup.Count, dto.YourPieces.Count);
    }

    [Fact]
    public async Task Handle_ValidRequest_OpponentPiecesContainsOpponentLineup()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.Equal(DefaultLineup.Count, dto.OpponentPieces.Count);
    }

    [Fact]
    public async Task Handle_ValidRequest_YourPieceNamesMatchCallerLineup()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: names match the lineup names (order may vary)
        var returnedNames = dto.YourPieces.Select(p => p.Name).OrderBy(n => n).ToList();
        var expectedNames = DefaultLineup.OrderBy(n => n).ToList();
        Assert.Equal(expectedNames, returnedNames);
    }

    [Fact]
    public async Task Handle_ValidRequest_PiecesNotYetPlacedHaveNullPosition()
    {
        // Arrange: game just started — no pieces placed yet
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: no piece is on the board yet, so positions are null
        Assert.All(dto.YourPieces, piece => Assert.Null(piece.Position));
    }

    [Fact]
    public async Task Handle_ValidRequest_PiecesNotYetPlacedAreNotOnBoard()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.All(dto.YourPieces, piece => Assert.False(piece.IsOnBoard));
    }

    [Fact]
    public async Task Handle_ValidRequest_PiecesHaveNonEmptyMovementType()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.All(dto.YourPieces, piece => Assert.False(string.IsNullOrEmpty(piece.MovementType)));
    }

    [Fact]
    public async Task Handle_ValidRequest_PiecesHavePositiveMaxDistance()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.All(dto.YourPieces, piece => Assert.True(piece.MaxDistance >= 1));
    }

    // ── AC 7 : yourPieces / opponentPieces are relative to calling bot ────────

    [Fact]
    public async Task Handle_CalledByPlayerTwo_YourPiecesContainsPlayerTwoPieces()
    {
        // Arrange: player 2 issues the query
        var (game, p1, p2) = NewStartedGame();
        var reg2 = MakeRegistration(game.Id, p2);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg2.Token, Arg.Any<CancellationToken>()).Returns(reg2);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg2.Token), CancellationToken.None);

        // Assert: YourPieces are from P2's lineup (same piece count)
        Assert.Equal(DefaultLineup.Count, dto.YourPieces.Count);
    }

    [Fact]
    public async Task Handle_CalledByPlayerTwo_OpponentPiecesContainsPlayerOnePieces()
    {
        // Arrange
        var (game, p1, p2) = NewStartedGame();
        var p2pieces = game.LineupPlayerTwo!.Pieces.Select(p => p.Id).ToHashSet();
        var reg2 = MakeRegistration(game.Id, p2);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg2.Token, Arg.Any<CancellationToken>()).Returns(reg2);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg2.Token), CancellationToken.None);

        // Assert: OpponentPieces should NOT include P2's pieces
        var opponentIds = dto.OpponentPieces.Select(p => p.PieceId).ToHashSet();
        Assert.Empty(opponentIds.Intersect(p2pieces));
    }

    // ── AC 8 : availableCoins only includes coin tiles ─────────────────────────

    [Fact]
    public async Task Handle_NoCoinsOnBoard_AvailableCoinsIsEmpty()
    {
        // Arrange: game just started, no coins spawned
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.Empty(dto.AvailableCoins);
    }

    [Fact]
    public async Task Handle_WithCoinsOnBoard_AvailableCoinsMatchesCoinCount()
    {
        // Arrange: spawn 3 coins during CoinSpawn phase
        var (game, p1, _) = NewStartedGame();
        game.SpawnCoins(new[]
        {
            (new Position(0, 0), CoinType.Silver),
            (new Position(3, 4), CoinType.Gold),
            (new Position(7, 7), CoinType.Silver)
        });

        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        Assert.Equal(3, dto.AvailableCoins.Count);
    }

    [Fact]
    public async Task Handle_WithCoinsOnBoard_CoinPositionsMatchBoardState()
    {
        // Arrange: spawn a silver coin at (2, 5)
        var (game, p1, _) = NewStartedGame();
        game.SpawnCoins(new[] { (new Position(2, 5), CoinType.Silver) });

        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        var coin = Assert.Single(dto.AvailableCoins);
        Assert.Equal(2, coin.Position.Row);
        Assert.Equal(5, coin.Position.Col);
    }

    [Fact]
    public async Task Handle_WithGoldCoin_AvailableCoinHasGoldType()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        game.SpawnCoins(new[] { (new Position(4, 4), CoinType.Gold) });

        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        var coin = Assert.Single(dto.AvailableCoins);
        Assert.Equal("Gold", coin.CoinType);
        Assert.Equal(3, coin.Value);
    }

    [Fact]
    public async Task Handle_WithSilverCoin_AvailableCoinHasSilverType()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        game.SpawnCoins(new[] { (new Position(1, 1), CoinType.Silver) });

        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert
        var coin = Assert.Single(dto.AvailableCoins);
        Assert.Equal("Silver", coin.CoinType);
        Assert.Equal(1, coin.Value);
    }

    [Fact]
    public async Task Handle_WithCoinOnBoard_TileOccupantHasCoinType()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        game.SpawnCoins(new[] { (new Position(3, 3), CoinType.Silver) });

        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: the tile at (3,3) should have a coin occupant
        var coinTile = dto.Board.Tiles.Single(t => t.Position.Row == 3 && t.Position.Col == 3);
        Assert.NotNull(coinTile.Occupant);
        Assert.Equal("coin", coinTile.Occupant.Type);
    }

    [Fact]
    public async Task Handle_EmptyBoard_AllTileOccupantsAreNull()
    {
        // Arrange: no coins, no pieces placed
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: all occupants null on an empty board
        Assert.All(dto.Board.Tiles, tile => Assert.Null(tile.Occupant));
    }

    // ── AC 5 : 404 when game not found ────────────────────────────────────────

    [Fact]
    public async Task Handle_GameNotFound_PropagatesGameNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var token = Guid.NewGuid();

        var botReg = new DomainBotReg(token, Guid.NewGuid(), gameId);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        botRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>()).Returns(botReg);
        gameRepo.GetByIdAsync(gameId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GameNotFoundException(gameId));

        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        await Assert.ThrowsAsync<GameNotFoundException>(() =>
            handler.Handle(new GetBoardStateQuery(gameId, token), CancellationToken.None));
    }

    // ── AC 6 : 403 when token is missing or invalid ────────────────────────────

    [Fact]
    public async Task Handle_TokenNotFoundInRegistry_ThrowsUnauthorizedGameAccessException()
    {
        // Arrange: bot repo returns null → token unknown
        var gameId = Guid.NewGuid();
        var token = Guid.NewGuid();

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        botRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>()).Returns((DomainBotReg?)null);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedGameAccessException>(() =>
            handler.Handle(new GetBoardStateQuery(gameId, token), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_TokenBelongsToDifferentGame_ThrowsUnauthorizedGameAccessException()
    {
        // Arrange: registration exists but is for a different game
        var requestedGameId = Guid.NewGuid();
        var differentGameId = Guid.NewGuid();
        var token = Guid.NewGuid();

        var botReg = new DomainBotReg(token, Guid.NewGuid(), differentGameId);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        botRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>()).Returns(botReg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedGameAccessException>(() =>
            handler.Handle(new GetBoardStateQuery(requestedGameId, token), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_TokenBelongsToDifferentGame_DoesNotLoadGame()
    {
        // Arrange: registration exists but is for a different game
        var requestedGameId = Guid.NewGuid();
        var token = Guid.NewGuid();

        var botReg = new DomainBotReg(token, Guid.NewGuid(), Guid.NewGuid()); // different game

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        botRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>()).Returns(botReg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act (will throw)
        await Assert.ThrowsAsync<UnauthorizedGameAccessException>(() =>
            handler.Handle(new GetBoardStateQuery(requestedGameId, token), CancellationToken.None));

        // Assert: game repo was never called since auth failed first
        await gameRepo.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    // ── ActivePlayer ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuringCoinSpawnPhase_ActivePlayerIsNull()
    {
        // Arrange
        var (game, p1, _) = NewStartedGame();
        var reg = MakeRegistration(game.Id, p1);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Assert: MovePhaseActivePlayer is null outside MovePhase
        Assert.Null(dto.ActivePlayer);
    }
}
