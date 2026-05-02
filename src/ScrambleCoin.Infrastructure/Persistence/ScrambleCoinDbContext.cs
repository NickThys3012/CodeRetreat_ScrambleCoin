using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Infrastructure.Persistence.Records;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for ScrambleCoin.
/// Maps the <see cref="GameRecord"/> persistence POCO to the <c>Games</c> table.
/// Complex domain types are stored as JSON columns to minimise table count.
/// </summary>
public class ScrambleCoinDbContext : DbContext
{
    public ScrambleCoinDbContext(DbContextOptions<ScrambleCoinDbContext> options)
        : base(options)
    {
    }

    /// <summary>The Games table — each row represents one <see cref="ScrambleCoin.Domain.Entities.Game"/> aggregate.</summary>
    public DbSet<GameRecord> Games => Set<GameRecord>();

    /// <summary>The BotRegistrations table — one row per bot that has joined a game.</summary>
    public DbSet<BotRegistrationRecord> BotRegistrations => Set<BotRegistrationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BotRegistrationRecord>(entity =>
        {
            entity.ToTable("BotRegistrations");
            entity.HasKey(b => b.Token);
            entity.Property(b => b.PlayerId).IsRequired();
            entity.Property(b => b.GameId).IsRequired();
        });

        modelBuilder.Entity<GameRecord>(entity =>
        {
            entity.ToTable("Games");
            entity.HasKey(g => g.Id);

            entity.Property(g => g.PlayerOne).IsRequired();
            entity.Property(g => g.PlayerTwo).IsRequired();
            entity.Property(g => g.Status).IsRequired();
            entity.Property(g => g.TurnNumber).IsRequired();
            entity.Property(g => g.CurrentPhase);
            entity.Property(g => g.MovePhaseActivePlayer);

            // JSON columns: stored as unicode text; EF Core provider picks the right column type.
            entity.Property(g => g.ScoresJson).IsRequired().HasColumnName("Scores");
            entity.Property(g => g.PiecesOnBoardJson).IsRequired().HasColumnName("PiecesOnBoard");
            entity.Property(g => g.PlacePhaseDoneJson).IsRequired().HasColumnName("PlacePhaseDone");
            entity.Property(g => g.MovedPieceIdsJson).IsRequired().HasColumnName("MovedPieceIds");
            entity.Property(g => g.LineupPlayerOneJson).HasColumnName("LineupPlayerOne");
            entity.Property(g => g.LineupPlayerTwoJson).HasColumnName("LineupPlayerTwo");
            entity.Property(g => g.BoardStateJson).IsRequired().HasColumnName("BoardState");
        });
    }
}
