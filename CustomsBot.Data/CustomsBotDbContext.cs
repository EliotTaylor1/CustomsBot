using CustomsBot.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomsBot.Data;

public class CustomsBotDbContext(DbContextOptions<CustomsBotDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<SeriesTeam> SeriesTeams => Set<SeriesTeam>();
    public DbSet<SeriesParticipant> SeriesParticipants => Set<SeriesParticipant>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();
    public DbSet<DraftAction> DraftActions => Set<DraftAction>();
    public DbSet<GamePlayerStats> GamePlayerStats => Set<GamePlayerStats>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.DiscordId).IsUnique();
            e.Property(p => p.DiscordUsername).HasMaxLength(64);
            e.Property(p => p.RiotId).HasMaxLength(128);
            e.Property(p => p.Puuid).HasMaxLength(128);
            e.HasIndex(p => p.Puuid);
        });

        b.Entity<Series>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(128);
            e.HasMany(s => s.Teams).WithOne(t => t.Series).HasForeignKey(t => t.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Participants).WithOne(p => p.Series).HasForeignKey(p => p.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Games).WithOne(g => g.Series).HasForeignKey(g => g.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SeriesTeam>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(128);
            e.HasOne(t => t.Captain).WithMany().HasForeignKey(t => t.CaptainPlayerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SeriesParticipant>(e =>
        {
            e.HasKey(p => new { p.SeriesId, p.PlayerId });
            e.HasOne(p => p.Player).WithMany().HasForeignKey(p => p.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Game>(e =>
        {
            e.HasKey(g => g.Id);
            e.HasIndex(g => new { g.SeriesId, g.GameNumber }).IsUnique();
            e.Property(g => g.TournamentCode).HasMaxLength(128);
            e.Property(g => g.RiotMatchId).HasMaxLength(64);

            // Side/winner FKs reference SeriesTeam; restrict to avoid multiple cascade paths.
            e.HasOne(g => g.BlueTeam).WithMany().HasForeignKey(g => g.BlueTeamId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.RedTeam).WithMany().HasForeignKey(g => g.RedTeamId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.WinnerTeam).WithMany().HasForeignKey(g => g.WinnerTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<GamePlayer>(e =>
        {
            e.HasKey(gp => gp.Id);
            e.HasIndex(gp => new { gp.GameId, gp.PlayerId }).IsUnique();
            e.HasOne(gp => gp.Game).WithMany(g => g.Players).HasForeignKey(gp => gp.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(gp => gp.Player).WithMany().HasForeignKey(gp => gp.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DraftAction>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.GameId, d.Sequence }).IsUnique();
            e.HasOne(d => d.Game).WithMany(g => g.DraftActions).HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Player).WithMany().HasForeignKey(d => d.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<GamePlayerStats>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.GameId, s.PlayerId }).IsUnique();
            e.Property(s => s.Raw).HasColumnType("jsonb");
            e.HasOne(s => s.Game).WithMany(g => g.Stats).HasForeignKey(s => s.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Player).WithMany().HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Store every enum as text so DB rows are self-describing (matches the plan's status labels).
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (clrType.IsEnum)
                {
                    b.Entity(entityType.ClrType).Property(property.Name)
                        .HasConversion<string>()
                        .HasMaxLength(32);
                }
            }
        }
    }
}
