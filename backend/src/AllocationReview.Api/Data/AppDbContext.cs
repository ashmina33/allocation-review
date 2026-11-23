using AllocationReview.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AllocationReview.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<AllocationRun> Runs => Set<AllocationRun>();
    public DbSet<AllocationGap> Gaps => Set<AllocationGap>();
    public DbSet<BatchAllocation> BatchAllocations => Set<BatchAllocation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables

        builder.Entity<AllocationRun>(e =>
        {
            e.ToTable("allocation_runs");
            e.Property(r => r.RunCode).HasMaxLength(32).IsRequired();
            e.HasIndex(r => r.RunCode).IsUnique();
            // Supports the history query: ORDER BY run_date DESC, id DESC with OFFSET/LIMIT.
            e.HasIndex(r => new { r.RunDate, r.Id }).IsDescending(true, true);
            e.HasMany(r => r.Gaps).WithOne(g => g.Run!).HasForeignKey(g => g.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AllocationGap>(e =>
        {
            e.ToTable("allocation_gaps");
            e.Property(g => g.GapCode).HasMaxLength(32).IsRequired();
            e.Property(g => g.Store).HasMaxLength(100).IsRequired();
            e.Property(g => g.Brand).HasMaxLength(100).IsRequired();
            e.Property(g => g.ProductType).HasMaxLength(100).IsRequired();
            e.Property(g => g.Shortfall).HasPrecision(12, 2);
            // A gap ID is unique within its run, not globally.
            e.HasIndex(g => new { g.RunId, g.GapCode }).IsUnique();
            e.HasMany(g => g.Allocations).WithOne(a => a.Gap!).HasForeignKey(a => a.GapId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BatchAllocation>(e =>
        {
            e.ToTable("batch_allocations");
            e.Property(a => a.BatchCode).HasMaxLength(32).IsRequired();
            e.HasIndex(a => a.GapId);
        });
    }
}
