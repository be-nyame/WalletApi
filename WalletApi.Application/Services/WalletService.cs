using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WalletApi.Application.DTOs.Wallet;
using WalletApi.Application.Interfaces;
using WalletApi.Domain.Entities;
using WalletApi.Infrastructure.Data;

namespace WalletApi.Application.Services;

public class WalletService : IWalletService
{
    private readonly WalletDbContext _db;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        WalletDbContext db,
        IPublishEndpoint bus,
        ILogger<WalletService> logger)
    {
        _db = db;
        _bus = bus;
        _logger = logger;
    }

    public async Task<WalletResponse> GetWalletAsync(
        Guid userId, CancellationToken ct = default)
    {
        var wallet = await _db.Wallets
                         .FirstOrDefaultAsync(w => w.UserId == userId && w.IsActive, ct)
                     ?? throw new KeyNotFoundException("Wallet not found.");

        return MapToResponse(wallet);
    }
    
    public async Task<WalletResponse> TopUpAsync(
        Guid userId, TopUpRequest req, CancellationToken ct = default)
    {
        if (req.Amount <= 0)
            throw new ArgumentException("Top-up amount must be greater than zero.");

        _logger.LogInformation(
            "TopUp started | UserId: {UserId} | Amount: {Amount}",
            userId, req.Amount);

        var wallet = await _db.Wallets
                         .FirstOrDefaultAsync(w => w.UserId == userId && w.IsActive, ct)
                     ?? throw new KeyNotFoundException("Wallet not found.");

        wallet.Credit(req.Amount);
        
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "TopUp completed | WalletId: {WalletId} | Amount: {Amount} | " +
            "NewBalance: {Balance}",
            wallet.Id, req.Amount, wallet.Balance);

        return MapToResponse(wallet);
    }
    private static WalletResponse MapToResponse(Wallet w) =>
        new(w.Id, w.Currency, w.Balance, w.IsActive, w.CreatedAt);
}