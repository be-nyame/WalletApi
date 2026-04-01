namespace WalletApi.IntegrationTests.Commons;

public record TestAuthResponse(
    string   AccessToken,
    string   RefreshToken,
    DateTime ExpiresAt,
    Guid     UserId);