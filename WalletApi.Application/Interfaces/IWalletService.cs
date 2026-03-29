using WalletApi.Application.DTOs.Wallet;

namespace WalletApi.Application.Interfaces;

public interface IWalletService
{
    Task<WalletResponse> GetWalletAsync(Guid userId, CancellationToken ct = default);
}