using MassTransit;
using Microsoft.Extensions.Logging;
using WalletApi.Application.Events;

namespace WalletApi.Application.Consumers;

public class TransferAuditConsumer : IConsumer<TransferCompletedEvent>
{
    private readonly ILogger<TransferAuditConsumer> _logger;

    public TransferAuditConsumer(
        ILogger<TransferAuditConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<TransferCompletedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "AUDIT | Transfer {Ref} | From: {Sender} | To: {Recipient} | " +
            "Amount: {Amount} {Currency} | At: {OccurredAt}",
            evt.TransferReference,
            evt.SenderWalletId,
            evt.RecipientWalletId,
            evt.Amount,
            evt.Currency,
            evt.OccurredAt);

        return Task.CompletedTask;
    }
}