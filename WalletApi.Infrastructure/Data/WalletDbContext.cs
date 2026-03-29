using Microsoft.EntityFrameworkCore;
using WalletApi.Domain.Entities;

namespace WalletApi.Infrastructure.Data;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        
        builder.Entity<Wallet>()
            .Property(w => w.Balance)
            .HasColumnType("numeric(18,4)")
            // PropertyAccessMode tells EF Core to use reflection to write to it
            // directly, bypassing the accessor. Without this, EF Core cannot 
            // hydrate Balance from the DB.
            .UsePropertyAccessMode(PropertyAccessMode.Property);
    }
}