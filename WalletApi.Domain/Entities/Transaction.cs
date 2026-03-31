using WalletApi.Domain.Common;
using WalletApi.Domain.Enums;

namespace WalletApi.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid WalletId { get; set; }
    public Guid? RecipientWalletId { get; set; }   // null for top-ups/withdrawals
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? Description { get; set; }
    // Unique per transaction (for tracing/logging)
    public string Reference { get; set; } = string.Empty;
    // Shared across related transactions (transfer group)
    public string TransactionGroupId { get; set; } = string.Empty;
    public Wallet Wallet { get; set; } = null!;
}