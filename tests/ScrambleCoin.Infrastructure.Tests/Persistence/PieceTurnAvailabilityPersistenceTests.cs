using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure.Tests.Persistence;

/// <summary>
/// Persistence round-trip tests for Issue #59 — verifies <see cref="Piece.AvailableFromTurn"/>
/// survives save+load through <see cref="GameRepository"/>, and that pre-#59 stored JSON
/// (missing the property) still deserialises without crashing.
/// </summary>
public sealed class PieceTurnAvailabilityPersistenceTests
{
    private static DbContextOptions<ScrambleCoinDbContext> BuildOptions(string dbName) =>
        new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

    /// <summary>
    /// Builds a started game where player one's lineup contains a mix of restricted
    /// (Elsa: turn 2, Merlin: turn 4) and unrestricted (Mickey, Goofy, Scrooge) pieces.
    /// </summary>
    private static (Game game, Guid p1, Guid p2) BuildStartedGameWithRestrictedLineup()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        string[] lineup1 = ["Mickey", "Elsa", "Merlin", "Goofy", "Scrooge"];
        string[] lineup2 = ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

        var pieces1 = lineup1.Select(n => PieceFactory.Create(n, p1)).ToList();
        var pieces2 = lineup2.Select(n => PieceFactory.Create(n, p2)).ToList();

        var game = new Game(Guid.NewGuid(), p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start();
        return (game, p1, p2);
    }

    [Fact]
    public async Task SaveThenLoad_RestrictedPiece_PreservesAvailableFromTurn()
    {
        var options = BuildOptions(nameof(SaveThenLoad_RestrictedPiece_PreservesAvailableFromTurn));
        var (game, _, _) = BuildStartedGameWithRestrictedLineup();

        await using (var writeCtx = new ScrambleCoinDbContext(options))
            await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        var elsa = loaded.LineupPlayerOne!.Pieces.Single(p => p.Name == "Elsa");
        Assert.Equal(2, elsa.AvailableFromTurn);
    }

    [Fact]
    public async Task SaveThenLoad_HighRestrictionPiece_PreservesAvailableFromTurn()
    {
        var options = BuildOptions(nameof(SaveThenLoad_HighRestrictionPiece_PreservesAvailableFromTurn));
        var (game, _, _) = BuildStartedGameWithRestrictedLineup();

        await using (var writeCtx = new ScrambleCoinDbContext(options))
            await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        var merlin = loaded.LineupPlayerOne!.Pieces.Single(p => p.Name == "Merlin");
        Assert.Equal(4, merlin.AvailableFromTurn);
    }

    [Fact]
    public async Task SaveThenLoad_UnrestrictedPiece_PreservesNullAvailableFromTurn()
    {
        var options = BuildOptions(nameof(SaveThenLoad_UnrestrictedPiece_PreservesNullAvailableFromTurn));
        var (game, _, _) = BuildStartedGameWithRestrictedLineup();

        await using (var writeCtx = new ScrambleCoinDbContext(options))
            await new GameRepository(writeCtx).SaveAsync(game);

        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        var mickey = loaded.LineupPlayerOne!.Pieces.Single(p => p.Name == "Mickey");
        Assert.Null(mickey.AvailableFromTurn);
    }

    /// <summary>
    /// Backward-compatibility: a stored lineup JSON that pre-dates Issue #59
    /// (and therefore lacks the <c>AvailableFromTurn</c> property entirely) must
    /// deserialise without error and yield <c>null</c> for that property.
    /// </summary>
    [Fact]
    public async Task Load_LegacyLineupJsonWithoutAvailableFromTurn_DeserialisesAsNull()
    {
        var options = BuildOptions(nameof(Load_LegacyLineupJsonWithoutAvailableFromTurn_DeserialisesAsNull));
        var (game, _, _) = BuildStartedGameWithRestrictedLineup();

        // Save normally, then strip the AvailableFromTurn property from the stored JSON
        // to simulate a row that was written before the field existed.
        await using (var writeCtx = new ScrambleCoinDbContext(options))
            await new GameRepository(writeCtx).SaveAsync(game);

        await using (var mutateCtx = new ScrambleCoinDbContext(options))
        {
            var record = await mutateCtx.Games.SingleAsync(g => g.Id == game.Id);
            // Remove every occurrence of `,"AvailableFromTurn":<value>` or `"AvailableFromTurn":<value>,`
            record.LineupPlayerOneJson = StripAvailableFromTurn(record.LineupPlayerOneJson!);
            record.LineupPlayerTwoJson = StripAvailableFromTurn(record.LineupPlayerTwoJson!);
            await mutateCtx.SaveChangesAsync();
        }

        // Sanity: legacy JSON really lacks the field.
        await using (var verifyCtx = new ScrambleCoinDbContext(options))
        {
            var raw = (await verifyCtx.Games.SingleAsync(g => g.Id == game.Id)).LineupPlayerOneJson!;
            Assert.DoesNotContain("AvailableFromTurn", raw, StringComparison.OrdinalIgnoreCase);
        }

        // Loading via the repository must not throw, and every piece's
        // AvailableFromTurn must default to null.
        await using var readCtx = new ScrambleCoinDbContext(options);
        var loaded = await new GameRepository(readCtx).GetByIdAsync(game.Id);

        Assert.All(loaded.LineupPlayerOne!.Pieces, p => Assert.Null(p.AvailableFromTurn));
        Assert.All(loaded.LineupPlayerTwo!.Pieces, p => Assert.Null(p.AvailableFromTurn));
    }

    /// <summary>
    /// Strips the <c>AvailableFromTurn</c> JSON property (whatever its value, including null)
    /// from a serialised lineup payload, handling leading- and trailing-comma cases.
    /// </summary>
    private static string StripAvailableFromTurn(string json)
    {
        // Matches: ,"AvailableFromTurn":<number-or-null>   OR   "AvailableFromTurn":<value>,
        var pattern = "(,\\s*\"AvailableFromTurn\"\\s*:\\s*(?:null|-?\\d+))" +
                      "|(\"AvailableFromTurn\"\\s*:\\s*(?:null|-?\\d+)\\s*,)";
        return System.Text.RegularExpressions.Regex.Replace(json, pattern, string.Empty);
    }
}
