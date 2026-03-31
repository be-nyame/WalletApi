using MassTransit;
using Microsoft.Extensions.Logging;
using WalletApi.Application.Events;

namespace WalletApi.Application.Consumers;

public class FraudDetectionConsumer : IConsumer<SuspiciousActivityDetectedEvent>
{
    private readonly ILogger<FraudDetectionConsumer> _logger;

    public FraudDetectionConsumer(
        ILogger<FraudDetectionConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<SuspiciousActivityDetectedEvent> context)
    {
        var evt = context.Message;

        _logger.LogWarning(
            "FRAUD ALERT | Wallet: {WalletId} | Reason: {Reason} | " +
            "Amount: {Amount} | At: {OccurredAt}",
            evt.WalletId, evt.Reason, evt.Amount, evt.OccurredAt);

        // To be replaced with real fraud scoring / wallet freeze logic
        return Task.CompletedTask;
    }
}