namespace WalletApi.Application.DTOs.Wallet;

public record TransactionResponse(
    Guid     Id,
    string   Reference,
    string?  TransactionGroupId,
    decimal  Amount,
    string   Currency,
    string   Type,
    string   Status,
    string?  Description,
    DateTime CreatedAt
);