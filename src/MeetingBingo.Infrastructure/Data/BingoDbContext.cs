using Microsoft.EntityFrameworkCore;

namespace MeetingBingo.Infrastructure.Data;

public sealed class BingoDbContext(DbContextOptions<BingoDbContext> options) : DbContext(options)
{
    public DbSet<GameRow> Games => Set<GameRow>();
    public DbSet<PhraseMarkRow> PhraseMarks => Set<PhraseMarkRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameRow>(game =>
        {
            game.ToTable("Games");
            game.HasKey(g => g.Id);
            game.Property(g => g.Id).ValueGeneratedNever();   // the id comes from the domain: it is the idempotency key
            game.HasIndex(g => g.EndedAt);                     // for the weekly stats query
            game.HasMany(g => g.Phrases).WithOne().HasForeignKey(p => p.GameId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PhraseMarkRow>(mark =>
        {
            mark.ToTable("PhraseMarks");
            mark.HasKey(p => new { p.GameId, p.PhraseId });
        });
    }
}
