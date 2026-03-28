namespace WalletApi.Application.DTOs.Wallet;

public record TopUpRequest(decimal Amount, string? Description);