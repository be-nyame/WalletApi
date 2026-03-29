using MassTransit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using WalletApi.Application.Services;
using WalletApi.Domain.Entities;
using WalletApi.Infrastructure.Data;

namespace WalletApi.Tests.Services;

public class WalletServiceTests : IDisposable
{
    private readonly WalletDbContext _db;
    private readonly WalletService _sut;

    public WalletServiceTests()
    {
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WalletDbContext(options);
        _sut = new WalletService(
            _db,
            Mock.Of<IPublishEndpoint>(),
            Mock.Of<ILogger<WalletService>>()
        );
    }

    private async Task<(User user, Wallet wallet)> SeedUserWithWalletAsync(
        decimal initialBalance = 0m, string currency = "USD")
    {
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@test.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };
        var wallet = new Wallet { UserId = user.Id, Currency = currency };

        if (initialBalance > 0)
            wallet.Credit(initialBalance);

        _db.Users.Add(user);
        _db.Wallets.Add(wallet);
        await _db.SaveChangesAsync();

        return (user, wallet);
    }

    [Fact]
    public async Task GetWalletAsync_WithValidUserId_ReturnsWallet()
    {
        var (user, wallet) = await SeedUserWithWalletAsync(100m);

        var result = await _sut.GetWalletAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(wallet.Id, result.Id);
        Assert.Equal(100m, result.Balance);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public async Task GetWalletAsync_WithUnknownUserId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetWalletAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetWalletAsync_WhenWalletIsInactive_ThrowsKeyNotFoundException()
    {
        var (user, wallet) = await SeedUserWithWalletAsync();
        wallet.IsActive = false;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetWalletAsync(user.Id));
    }
    
    public void Dispose() => _db.Dispose();
}