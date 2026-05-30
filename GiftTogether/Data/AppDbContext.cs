using GiftTogether.Models;
using Microsoft.EntityFrameworkCore;

namespace GiftTogether.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Registry> Registries => Set<Registry>();
    public DbSet<GiftGoal> GiftGoals => Set<GiftGoal>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<PendingPayment> PendingPayments => Set<PendingPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Unique email per user
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Unique slug per registry
        modelBuilder.Entity<Registry>()
            .HasIndex(r => r.Slug)
            .IsUnique();

        // Decimal precision for SQLite
        modelBuilder.Entity<GiftGoal>()
            .Property(g => g.TargetAmount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Contribution>()
            .Property(c => c.Amount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<PendingPayment>()
            .HasIndex(p => p.Reference)
            .IsUnique();

        modelBuilder.Entity<PendingPayment>()
            .Property(p => p.ContributionAmount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<PendingPayment>()
            .Property(p => p.GrossAmount)
            .HasColumnType("decimal(18,2)");
    }
}
