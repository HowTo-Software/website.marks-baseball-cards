using MarksBaseballCards.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarksBaseballCards.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardPhoto> CardPhotos => Set<CardPhoto>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CardHistory> History => Set<CardHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Card>(e =>
        {
            e.Property(c => c.Description).HasMaxLength(2000);
            e.HasIndex(c => new { c.IsPublished, c.IsForSale, c.IsSold, c.Id });
            e.HasIndex(c => new { c.IsPublished, c.Id });
            e.Property(c => c.CardNumber).HasMaxLength(20).IsRequired();
            e.Property(c => c.PlayerName).HasMaxLength(120).IsRequired();
            e.Property(c => c.Condition).HasMaxLength(60);
            e.Property(c => c.ListingNotes).HasMaxLength(500);
            e.Property(c => c.StripeCheckoutSessionId).HasMaxLength(200);
            e.Property(c => c.Price).HasPrecision(18, 2);
            e.Property(c => c.SoldPrice).HasPrecision(18, 2);
            e.Property(c => c.RowVersion).IsRowVersion();
            e.HasIndex(c => c.CardNumber).IsUnique();
            e.HasIndex(c => c.IsForSale);
        });

        b.Entity<CardPhoto>(e =>
        {
            e.Property(p => p.Caption).HasMaxLength(200).IsRequired();
            e.HasOne(p => p.Card).WithMany(c => c.Photos).HasForeignKey(p => p.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.CardId, p.Position, p.Id });
        });

        b.Entity<AppUser>(e =>
        {
            e.Property(u => u.Username).HasMaxLength(64).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(u => u.Role).HasMaxLength(32).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
        });

        b.Entity<CardHistory>(e =>
        {
            e.Property(h => h.CardNumber).HasMaxLength(20);
            e.Property(h => h.PlayerName).HasMaxLength(120);
            e.Property(h => h.Action).HasMaxLength(32).IsRequired();
            e.Property(h => h.Details).HasMaxLength(1000);
            e.Property(h => h.ChangedBy).HasMaxLength(64).IsRequired();
            e.HasOne(h => h.Card)
                .WithMany()
                .HasForeignKey(h => h.CardId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(h => h.ChangedAtUtc);
        });
    }
}
