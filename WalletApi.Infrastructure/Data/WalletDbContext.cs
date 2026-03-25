using Microsoft.EntityFrameworkCore;
using WalletApi.Domain.Entities;

namespace WalletApi.Infrastructure.Data;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // User
        builder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
    }
}