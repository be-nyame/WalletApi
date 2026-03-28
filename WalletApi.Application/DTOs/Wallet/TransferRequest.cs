namespace WalletApi.Application.DTOs.Wallet;

public record TransferRequest(Guid RecipientWalletId, decimal Amount, string? Description);