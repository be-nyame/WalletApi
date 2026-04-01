namespace WalletApi.IntegrationTests.Commons;

public record TestAuthResponse(
    string   AccessToken,
    string   RefreshToken,
    DateTime ExpiresAt,
    Guid     UserId);
    
public record WalletResponse(
    Guid     Id,
    string   Currency,
    decimal  Balance,
    bool     IsActive,
    DateTime CreatedAt);

public record TransactionResponse(
    Guid     Id,
    string   Reference,
    string?  TransactionGroupId,
    decimal  Amount,
    string   Currency,
    string   Type,
    string   Status,
    string   Description,
    DateTime CreatedAt);