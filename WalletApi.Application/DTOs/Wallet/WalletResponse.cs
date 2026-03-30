namespace WalletApi.Application.DTOs.Wallet;

public record WalletResponse(
    Guid Id,
    string Currency,
    decimal Balance,
    bool IsActive,
    DateTime CreatedAt
);