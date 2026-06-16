using Bunit;
using ScrambleCoin.Application.Games.GetBoardState;
using ScrambleCoin.Web.Shared;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// bUnit component tests for <c>GameBoard.razor</c> — Issue #107.
///
/// Verifies that PlayerOne pieces always render with the blue <c>piece-p1</c> class,
/// while PlayerTwo pieces render as the purple <c>piece-villain</c> in solo mode and
/// the red <c>piece-p2</c> in bot-vs-bot mode.
///
/// In <c>GameBoard</c>, <c>YourPieces</c> are PlayerOne and <c>OpponentPieces</c> are PlayerTwo.
/// </summary>
public sealed class GameBoardComponentTests
{
    private static PieceDto PlacedPiece(string name, int row, int col) =>
        new(
            PieceId: Guid.NewGuid(),
            Name: name,
            Position: new PositionDto(row, col),
            MovementType: "Orthogonal",
            MaxDistance: 3,
            MovesPerTurn: 1,
            IsOnBoard: true,
            AvailableFromTurn: null);

    /// <summary>Builds a board state with one placed PlayerOne piece and one placed PlayerTwo piece.</summary>
    private static BoardStateDto BuildBoardState(bool isSoloMode) =>
        new(
            Turn: 1,
            Phase: "MovePhase",
            YourScore: 0,
            OpponentScore: 0,
            Board: new BoardDto([]),
            YourPieces: [PlacedPiece("Mickey", 0, 0)],
            OpponentPieces: [PlacedPiece("Maleficent", 7, 7)],
            AvailableCoins: [],
            ActivePlayer: null,
            IsSoloMode: isSoloMode,
            VillainId: isSoloMode ? "elsa" : null);

    [Fact]
    public void GameBoard_SoloMode_RendersPlayerOnePieceAsBlue()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<GameBoard>(parameters => parameters
            .Add(p => p.BoardState, BuildBoardState(isSoloMode: true))
            .Add(p => p.IsSoloMode, true));

        Assert.Contains("piece-bubble piece-p1", cut.Markup);
    }

    [Fact]
    public void GameBoard_SoloMode_RendersPlayerTwoPieceAsVillain()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<GameBoard>(parameters => parameters
            .Add(p => p.BoardState, BuildBoardState(isSoloMode: true))
            .Add(p => p.IsSoloMode, true));

        Assert.Contains("piece-bubble piece-villain", cut.Markup);
        Assert.DoesNotContain("piece-bubble piece-p2", cut.Markup);
    }

    [Fact]
    public void GameBoard_BotVsBot_RendersPlayerTwoPieceAsRed()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<GameBoard>(parameters => parameters
            .Add(p => p.BoardState, BuildBoardState(isSoloMode: false))
            .Add(p => p.IsSoloMode, false));

        Assert.Contains("piece-bubble piece-p2", cut.Markup);
        Assert.DoesNotContain("piece-bubble piece-villain", cut.Markup);
    }

    [Fact]
    public void GameBoard_BotVsBot_RendersPlayerOnePieceAsBlue()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<GameBoard>(parameters => parameters
            .Add(p => p.BoardState, BuildBoardState(isSoloMode: false))
            .Add(p => p.IsSoloMode, false));

        Assert.Contains("piece-bubble piece-p1", cut.Markup);
    }
}
