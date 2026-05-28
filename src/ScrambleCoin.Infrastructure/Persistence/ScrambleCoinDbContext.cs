using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Infrastructure.Persistence.Records;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for ScrambleCoin.
/// Maps the <see cref="GameRecord"/> persistence POCO to the <c>Games</c> table.
/// Complex domain types are stored as JSON columns to minimize table count.
/// </summary>
public class ScrambleCoinDbContext : DbContext, IUnitOfWork
{
    public ScrambleCoinDbContext(DbContextOptions<ScrambleCoinDbContext> options)
        : base(options)
    {
    }

    /// <summary>The Games table — each row represents one <see cref="ScrambleCoin.Domain.Entities.Game"/> aggregate.</summary>
    public DbSet<GameRecord> Games => Set<GameRecord>();

    /// <summary>The BotRegistrations table — one row per bot that has joined a game.</summary>
    public DbSet<BotRegistrationRecord> BotRegistrations => Set<BotRegistrationRecord>();

    /// <summary>The VillainTreeNodes table — villain unlock tree nodes.</summary>
    public DbSet<VillainTreeNode> VillainTreeNodes => Set<VillainTreeNode>();

    /// <summary>The BotUnlocks table — records of villain defeats and piece unlocks.</summary>
    public DbSet<BotUnlock> BotUnlocks => Set<BotUnlock>();

    /// <summary>The VillainNodeParents join table — DAG edges (parent→child).</summary>
    public DbSet<VillainNodeParent> VillainNodeParents => Set<VillainNodeParent>();

    /// <summary>The Tournaments table — one row per tournament aggregate.</summary>
    public DbSet<TournamentRecord> Tournaments => Set<TournamentRecord>();

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
            entity.Property(g => g.GameMode).IsRequired();
            entity.Property(g => g.VillainId);
            entity.Property(g => g.TurnNumber).IsRequired();
            entity.Property(g => g.CurrentPhase);
            entity.Property(g => g.MovePhaseActivePlayer);

            // JSON columns: stored as Unicode text; EF Core provider picks the right column type.
            entity.Property(g => g.ScoresJson).IsRequired().HasColumnName("Scores");
            entity.Property(g => g.PiecesOnBoardJson).IsRequired().HasColumnName("PiecesOnBoard");
            entity.Property(g => g.PlacePhaseDoneJson).IsRequired().HasColumnName("PlacePhaseDone");
            entity.Property(g => g.MovedPieceIdsJson).IsRequired().HasColumnName("MovedPieceIds");
            entity.Property(g => g.LineupPlayerOneJson).HasColumnName("LineupPlayerOne");
            entity.Property(g => g.LineupPlayerTwoJson).HasColumnName("LineupPlayerTwo");
            entity.Property(g => g.BoardStateJson).IsRequired().HasColumnName("BoardState");
        });

        modelBuilder.Entity<VillainTreeNode>(entity =>
        {
            entity.ToTable("VillainTreeNodes");
            entity.HasKey(v => v.Id);

            entity.Property(v => v.VillainId).IsRequired().HasMaxLength(100);
            entity.Property(v => v.VillainName).IsRequired().HasMaxLength(200);
            entity.Property(v => v.UnlockedPieceId).HasMaxLength(100);
            entity.Property(v => v.DisplayOrder).IsRequired();
            entity.Property(v => v.CreatedAtUtc).IsRequired();

            entity.HasIndex(v => v.VillainId).IsUnique();

            // One VillainTreeNode has many parent-link rows, joined on VillainId (not Guid PK)
            entity.HasMany(v => v.ParentLinks)
                  .WithOne()
                  .HasForeignKey(p => p.ChildVillainId)
                  .HasPrincipalKey(v => v.VillainId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VillainNodeParent>(entity =>
        {
            entity.ToTable("VillainNodeParents");
            entity.HasKey(p => new { p.ChildVillainId, p.ParentVillainId });
            entity.Property(p => p.ChildVillainId).IsRequired().HasMaxLength(100);
            entity.Property(p => p.ParentVillainId).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<BotUnlock>(entity =>
        {
            entity.ToTable("BotUnlocks");
            entity.HasKey(bu => bu.Id);

            entity.Property(bu => bu.BotId).IsRequired();
            entity.Property(bu => bu.VillainId).IsRequired().HasMaxLength(100);
            entity.Property(bu => bu.UnlockedPieceId).HasMaxLength(100);
            entity.Property(bu => bu.DefeatedAtUtc).IsRequired();

            // Foreign key to VillainTreeNodes
            entity.HasOne<VillainTreeNode>()
                .WithMany()
                .HasForeignKey(bu => bu.VillainId)
                .HasPrincipalKey(v => v.VillainId);

            // Index on (BotId, VillainId): non-unique to allow re-challenges
            entity.HasIndex(bu => new { bu.BotId, bu.VillainId }).IsUnique(false);
        });

        modelBuilder.Entity<TournamentRecord>(entity =>
        {
            entity.ToTable("Tournaments");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
            entity.Property(t => t.MaxParticipants).IsRequired();
            entity.Property(t => t.TopN).IsRequired();
            entity.Property(t => t.Status).IsRequired();
            entity.Property(t => t.WinnerId);
            entity.Property(t => t.CreatedAtUtc).IsRequired();

            // JSON columns — stored as Unicode text
            entity.Property(t => t.ParticipantsJson).IsRequired().HasColumnName("Participants");
            entity.Property(t => t.GroupMatchesJson).IsRequired().HasColumnName("GroupMatches");
            entity.Property(t => t.KnockoutMatchesJson).IsRequired().HasColumnName("KnockoutMatches");
        });
    }
}
