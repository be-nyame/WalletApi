using MassTransit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using WalletApi.Application.Services;
using WalletApi.Application.DTOs.Wallet;
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
    
    [Fact]
     public async Task TopUpAsync_WithValidAmount_IncreasesBalance()
     {
         var (user, _) = await SeedUserWithWalletAsync();

         var result = await _sut.TopUpAsync(user.Id, new TopUpRequest(250m, "Test top-up"));

         Assert.Equal(250m, result.Balance);
     }

     [Fact]
     public async Task TopUpAsync_WithNegativeAmount_ThrowsArgumentException()
     {
         var (user, _) = await SeedUserWithWalletAsync();

         await Assert.ThrowsAsync<ArgumentException>(
             () => _sut.TopUpAsync(user.Id, new TopUpRequest(-50m, null)));
     }

     [Fact]
     public async Task TopUpAsync_WithZeroAmount_ThrowsArgumentException()
     {
         var (user, _) = await SeedUserWithWalletAsync();

         await Assert.ThrowsAsync<ArgumentException>(
             () => _sut.TopUpAsync(user.Id, new TopUpRequest(0m, null)));
     }

     [Fact]
     public async Task TopUpAsync_MultipleTopUps_AccumulatesBalance()
     {
         var (user, _) = await SeedUserWithWalletAsync();

         await _sut.TopUpAsync(user.Id, new TopUpRequest(100m, null));
         await _sut.TopUpAsync(user.Id, new TopUpRequest(200m, null));
         var result = await _sut.TopUpAsync(user.Id, new TopUpRequest(50m, null));

         Assert.Equal(350m, result.Balance);
     }
     
     [Fact]
     public async Task TransferAsync_WithSufficientFunds_UpdatesBothBalances()
     {
         var (sender,    senderWallet)    = await SeedUserWithWalletAsync(500m);
         var (recipient, recipientWallet) = await SeedUserWithWalletAsync(0m);

         await _sut.TransferAsync(sender.Id,
             new TransferRequest(recipientWallet.Id, 200m, "Payment"));

         await _db.Entry(senderWallet).ReloadAsync();
         await _db.Entry(recipientWallet).ReloadAsync();

         Assert.Equal(300m, senderWallet.Balance);
         Assert.Equal(200m, recipientWallet.Balance);
     }

     [Fact]
     public async Task TransferAsync_WithInsufficientFunds_ThrowsInvalidOperationException()
     {
         var (sender,    _)               = await SeedUserWithWalletAsync(50m);
         var (recipient, recipientWallet) = await SeedUserWithWalletAsync();

         await Assert.ThrowsAsync<InvalidOperationException>(
             () => _sut.TransferAsync(sender.Id,
                 new TransferRequest(recipientWallet.Id, 200m, null)));
     }

     [Fact]
     public async Task TransferAsync_WithInsufficientFunds_DoesNotModifyBalances()
     {
         var (sender,    senderWallet)    = await SeedUserWithWalletAsync(50m);
         var (recipient, recipientWallet) = await SeedUserWithWalletAsync(100m);

         try { await _sut.TransferAsync(sender.Id, new TransferRequest(recipientWallet.Id, 200m, null)); }
         catch (InvalidOperationException) { /* expected */ }

         await _db.Entry(senderWallet).ReloadAsync();
         await _db.Entry(recipientWallet).ReloadAsync();

         Assert.Equal(50m,  senderWallet.Balance);
         Assert.Equal(100m, recipientWallet.Balance);
     }

     [Fact]
     public async Task TransferAsync_WithZeroAmount_ThrowsArgumentException()
     {
         var (sender,    _)               = await SeedUserWithWalletAsync(500m);
         var (recipient, recipientWallet) = await SeedUserWithWalletAsync();

         await Assert.ThrowsAsync<ArgumentException>(
             () => _sut.TransferAsync(sender.Id,
                 new TransferRequest(recipientWallet.Id, 0m, null)));
     }

     [Fact]
     public async Task TransferAsync_ToNonExistentWallet_ThrowsKeyNotFoundException()
     {
         var (sender, _) = await SeedUserWithWalletAsync(500m);
     
         await Assert.ThrowsAsync<KeyNotFoundException>(
             () => _sut.TransferAsync(sender.Id,
                 new TransferRequest(Guid.NewGuid(), 100m, null)));
     }
    
     [Fact]
     public async Task TransferAsync_CrossCurrency_ThrowsInvalidOperationException()
     {
         var (sender,    _)               = await SeedUserWithWalletAsync(500m, "USD");
         var (recipient, recipientWallet) = await SeedUserWithWalletAsync(0m,   "EUR");

         await Assert.ThrowsAsync<InvalidOperationException>(
             () => _sut.TransferAsync(sender.Id,
                 new TransferRequest(recipientWallet.Id, 100m, null)));
     }
    
    public void Dispose() => _db.Dispose();
}