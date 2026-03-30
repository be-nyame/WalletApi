using WalletApi.Domain.Common;

namespace WalletApi.Domain.Entities;

public class Wallet : BaseEntity
{
    public Guid UserId { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal Balance { get; private set; } = 0m;
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    // Encapsulate balance mutation
    public void Credit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Credit amount must be positive.");
        Balance += amount;
    }

    public void Debit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Debit amount must be positive.");
        if (amount > Balance) throw new InvalidOperationException("Insufficient funds.");
        Balance -= amount;
    }
}