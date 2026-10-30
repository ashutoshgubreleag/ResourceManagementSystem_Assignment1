using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ApexPost.Database.Models;
using ApexPost.Repositories;
using ApexPost.Services.Events;

namespace ApexPost.Services.Notifications;

/// <summary>
/// Subscribes to business events and sends (simulated) notifications.
///
/// Event handlers run synchronously on the request thread, so they must be quick and must not
/// block on I/O. Each handler only writes to a bounded <see cref="Channel{T}"/>; this background
/// service drains the channel and persists asynchronously. Bounded capacity means a burst of
/// events cannot exhaust memory (oldest notifications are dropped and logged instead).
/// </summary>
public sealed partial class NotificationDispatcher : BackgroundService
{
    private readonly IApexPostEvents _events;
    private readonly IBaseEntityRepository<NotificationEntity> _notifications;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly TimeProvider _clock;
    private readonly Channel<NotificationEntity> _queue = Channel.CreateBounded<NotificationEntity>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public NotificationDispatcher(
        IApexPostEvents events,
        IBaseEntityRepository<NotificationEntity> notifications,
        ILogger<NotificationDispatcher> logger,
        TimeProvider clock)
    {
        _events = events;
        _notifications = notifications;
        _logger = logger;
        _clock = clock;

        // Subscribe immediately (not in ExecuteAsync) so no event raised during startup is missed.
        _events.StockLow += OnStockLow;
        _events.MoneyOrderIssued += OnMoneyOrderIssued;
        _events.TransactionVoided += OnTransactionVoided;
    }

    public override void Dispose()
    {
        _events.StockLow -= OnStockLow;
        _events.MoneyOrderIssued -= OnMoneyOrderIssued;
        _events.TransactionVoided -= OnTransactionVoided;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var notification in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await _notifications.AddAsync(notification, stoppingToken).ConfigureAwait(false);
                    LogNotificationSent(notification.Type, notification.Recipient, notification.Message, notification.CorrelationId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed notification must never take the dispatcher down.
                    LogNotificationFailed(ex, notification.Type);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private void OnStockLow(object? sender, StockLowEventArgs e) => Enqueue(new NotificationEntity
    {
        Type = NotificationType.LowStock,
        BranchCode = e.BranchCode,
        Recipient = $"manager.{e.BranchCode.Value.ToLowerInvariant()}@apexpost.local",
        Message = $"Low stock at {e.BranchCode}: {e.ProductName} ({e.Sku}) has {e.QuantityOnHand} left (reorder at {e.ReorderThreshold}).",
        CorrelationId = e.CorrelationId,
    });

    private void OnMoneyOrderIssued(object? sender, MoneyOrderIssuedEventArgs e)
    {
        if (!e.IsHighValue)
        {
            return;
        }

        Enqueue(new NotificationEntity
        {
            Type = NotificationType.HighValueMoneyOrder,
            BranchCode = e.BranchCode,
            Recipient = "compliance@apexpost.local",
            Message = $"High-value money order {e.SerialNumber} for {e.Amount} issued at {e.BranchCode}.",
            CorrelationId = e.CorrelationId,
        });
    }

    private void OnTransactionVoided(object? sender, TransactionVoidedEventArgs e) => Enqueue(new NotificationEntity
    {
        Type = e.IsRefund ? NotificationType.TransactionRefunded : NotificationType.TransactionVoided,
        BranchCode = e.BranchCode,
        Recipient = "audit@apexpost.local",
        Message = $"Transaction {e.TransactionId} at {e.BranchCode} was {(e.IsRefund ? "refunded" : "voided")} by staff {e.ManagerStaffId} ({e.Total}). Reason: {e.Reason}",
        CorrelationId = e.CorrelationId,
    });

    private void Enqueue(NotificationEntity notification)
    {
        notification.CreatedAt = _clock.GetUtcNow();
        if (!_queue.Writer.TryWrite(notification))
        {
            LogNotificationDropped(notification.Type);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[NOTIFY] {Type} to {Recipient}: {Message} (CorrelationId {CorrelationId})")]
    private partial void LogNotificationSent(NotificationType type, string recipient, string message, string correlationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send {Type} notification")]
    private partial void LogNotificationFailed(Exception exception, NotificationType type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification queue full; dropped {Type} notification")]
    private partial void LogNotificationDropped(NotificationType type);
}
