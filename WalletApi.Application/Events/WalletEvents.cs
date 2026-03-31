namespace WalletApi.Application.Events;

public record TransferCompletedEvent(
    string   TransferReference,
    Guid   SenderWalletId,
    Guid   RecipientWalletId,
    decimal Amount,
    string  Currency,
    string  SenderEmail,
    DateTime OccurredAt
);

public record TopUpCompletedEvent(
    Guid    WalletId,
    Guid    UserId,
    string  UserEmail,
    decimal Amount,
    string  Currency,
    string  Reference,
    DateTime OccurredAt
);

public record SuspiciousActivityDetectedEvent(
    Guid   WalletId,
    string Reason,
    decimal Amount,
    DateTime OccurredAt
);