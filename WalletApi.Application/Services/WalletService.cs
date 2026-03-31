using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WalletApi.Application.DTOs.Wallet;
using WalletApi.Application.Interfaces;
using WalletApi.Domain.Entities;
using WalletApi.Domain.Enums;
using WalletApi.Infrastructure.Data;

namespace WalletApi.Application.Services;

public class WalletService : IWalletService
{
    private readonly WalletDbContext       _db;
    private readonly IPublishEndpoint      _bus;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        WalletDbContext        db,
        IPublishEndpoint       bus,
        ILogger<WalletService> logger)
    {
        _db     = db;
        _bus    = bus;
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

        var tx = new Transaction
        {
            WalletId    = wallet.Id,
            Amount      = req.Amount,
            Currency    = wallet.Currency,
            Type        = TransactionType.TopUp,
            Status      = TransactionStatus.Completed,
            Description = req.Description ?? "Top-up",
            Reference   = "TU-" + Guid.NewGuid().ToString("N")[..8].ToUpper()
        };

        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "TopUp completed | WalletId: {WalletId} | Amount: {Amount} | " +
            "NewBalance: {Balance} | Reference: {Reference}",
            wallet.Id, req.Amount, wallet.Balance, tx.Reference);

        return MapToResponse(wallet);
    }

    public async Task TransferAsync(
        Guid senderUserId, TransferRequest req, CancellationToken ct = default)
    {
        if (req.Amount <= 0)
            throw new ArgumentException("Transfer amount must be greater than zero.");

        _logger.LogInformation(
            "Transfer started | SenderUserId: {SenderUserId} | " +
            "RecipientWalletId: {RecipientWalletId} | Amount: {Amount}",
            senderUserId, req.RecipientWalletId, req.Amount);

        var supportsTransactions = _db.Database.ProviderName !=
            "Microsoft.EntityFrameworkCore.InMemory";

        // ── Pre-lock validation ───────────────────────────────────────────────
        // Resolve wallet IDs and perform cheap business checks before opening
        // the transaction and acquiring any row locks. This keeps the lock
        // window as short as possible and avoids holding locks while throwing
        // validation errors.

        var senderMeta = await _db.Wallets
            .Where(w => w.UserId == senderUserId && w.IsActive)
            .Select(w => new { w.Id, w.Currency })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Sender wallet not found.");

        var recipientMeta = await _db.Wallets
            .Where(w => w.Id == req.RecipientWalletId && w.IsActive)
            .Select(w => new { w.Id, w.Currency })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Recipient wallet not found.");

        if (senderMeta.Id == recipientMeta.Id)
            throw new InvalidOperationException(
                "Cannot transfer funds to your own wallet.");

        if (senderMeta.Currency != recipientMeta.Currency)
            throw new InvalidOperationException(
                "Cross-currency transfers are not supported yet.");

        // ── Consistent lock ordering ──────────────────────────────────────────
        // Always acquire row locks in ascending GUID order regardless of which
        // wallet is the sender and which is the recipient. Any two concurrent
        // transfers involving the same pair of wallets will therefore always
        // request the locks in the same sequence, eliminating the cyclic
        // wait that causes deadlocks.

        var (firstId, secondId) = senderMeta.Id.CompareTo(recipientMeta.Id) < 0
            ? (senderMeta.Id, recipientMeta.Id)
            : (recipientMeta.Id, senderMeta.Id);

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? dbTx = null;

        if (supportsTransactions)
            dbTx = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            Wallet sender;
            Wallet recipient;

            if (supportsTransactions)
            {
                // Lock both rows in consistent order via SELECT … FOR UPDATE.
                // EF Core hydrates the full entity (including the private
                // Balance setter) from the raw SQL result set exactly as it
                // would from a LINQ query.
                var first = await _db.Wallets
                    .FromSqlRaw(
                        "SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE",
                        firstId)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new KeyNotFoundException("Wallet not found.");

                var second = await _db.Wallets
                    .FromSqlRaw(
                        "SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE",
                        secondId)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new KeyNotFoundException("Wallet not found.");

                // Re-assign to sender/recipient semantics after locking.
                sender    = first.Id == senderMeta.Id    ? first : second;
                recipient = first.Id == recipientMeta.Id ? first : second;
            }
            else
            {
                // InMemory provider: fall back to standard LINQ — no locking
                // needed in single-process test scenarios.
                sender = await _db.Wallets
                    .FirstOrDefaultAsync(w => w.Id == senderMeta.Id, ct)
                    ?? throw new KeyNotFoundException("Sender wallet not found.");

                recipient = await _db.Wallets
                    .FirstOrDefaultAsync(w => w.Id == recipientMeta.Id, ct)
                    ?? throw new KeyNotFoundException("Recipient wallet not found.");
            }

            sender.Debit(req.Amount);
            recipient.Credit(req.Amount);

            var groupId         = Guid.NewGuid().ToString("N");
            var debitReference  = Guid.NewGuid().ToString("N");
            var creditReference = Guid.NewGuid().ToString("N");

            _db.Transactions.AddRange(
                new Transaction
                {
                    WalletId           = sender.Id,
                    RecipientWalletId  = recipient.Id,
                    Amount             = req.Amount,
                    Currency           = sender.Currency,
                    Type               = TransactionType.Transfer,
                    Status             = TransactionStatus.Completed,
                    Description        = req.Description ?? "Transfer sent",
                    Reference          = debitReference,
                    TransactionGroupId = groupId
                },
                new Transaction
                {
                    WalletId           = recipient.Id,
                    RecipientWalletId  = sender.Id,
                    Amount             = req.Amount,
                    Currency           = sender.Currency,
                    Type               = TransactionType.Transfer,
                    Status             = TransactionStatus.Completed,
                    Description        = $"Transfer received from wallet {sender.Id}",
                    Reference          = creditReference,
                    TransactionGroupId = groupId
                }
            );

            await _db.SaveChangesAsync(ct);

            if (dbTx != null)
                await dbTx.CommitAsync(ct);

            _logger.LogInformation(
                "Transfer completed | GroupId: {GroupId} | " +
                "SenderWallet: {SenderWalletId} | RecipientWallet: {RecipientWalletId} | " +
                "Amount: {Amount} | SenderNewBalance: {SenderBalance}",
                groupId, sender.Id, recipient.Id,
                req.Amount, sender.Balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Transfer failed | SenderUserId: {SenderUserId} | Amount: {Amount}",
                senderUserId, req.Amount);

            if (dbTx != null)
                await dbTx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (dbTx != null)
                await dbTx.DisposeAsync();
        }
    }

    public async Task<List<TransactionResponse>> GetTransactionsAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId && w.IsActive, ct)
            ?? throw new KeyNotFoundException("Wallet not found.");

        return await _db.Transactions
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionResponse(
                t.Id,
                t.Reference,
                t.TransactionGroupId,
                t.Amount,
                t.Currency,
                t.Type.ToString(),
                t.Status.ToString(),
                t.Description,
                t.CreatedAt))
            .ToListAsync(ct);
    }

    private static WalletResponse MapToResponse(Wallet w) =>
        new(w.Id, w.Currency, w.Balance, w.IsActive, w.CreatedAt);
}