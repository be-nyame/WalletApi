using WalletApi.Application.DTOs.Wallet;

namespace WalletApi.Application.Interfaces;

public interface IWalletService
{
    Task<WalletResponse> GetWalletAsync(Guid userId, CancellationToken ct = default);
    Task<WalletResponse> TopUpAsync(Guid userId, TopUpRequest request, CancellationToken ct = default);
    Task TransferAsync(Guid senderUserId, TransferRequest request, CancellationToken ct = default);
    Task<List<TransactionResponse>> GetTransactionsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
}