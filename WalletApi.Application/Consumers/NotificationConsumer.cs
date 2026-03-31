using MassTransit;
using Microsoft.Extensions.Logging;
using WalletApi.Application.Events;

namespace WalletApi.Application.Consumers;

public class TransferNotificationConsumer : IConsumer<TransferCompletedEvent>
{
    private readonly ILogger<TransferNotificationConsumer> _logger;

    public TransferNotificationConsumer(
        ILogger<TransferNotificationConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<TransferCompletedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Transfer notification | Ref: {Ref} | Amount: {Amount} {Currency}",
            evt.TransferReference, evt.Amount, evt.Currency);

        // To be replaced with real email sending
        return Task.CompletedTask;
    }
}

public class TopUpNotificationConsumer : IConsumer<TopUpCompletedEvent>
{
    private readonly ILogger<TopUpNotificationConsumer> _logger;

    public TopUpNotificationConsumer(
        ILogger<TopUpNotificationConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<TopUpCompletedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Top-up notification | Wallet: {WalletId} | Amount: {Amount} {Currency}",
            evt.WalletId, evt.Amount, evt.Currency);

        return Task.CompletedTask;
    }
}