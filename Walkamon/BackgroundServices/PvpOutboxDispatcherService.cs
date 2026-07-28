using DAL.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Walkamon.Hubs;

namespace Walkamon.BackgroundServices;

public sealed class PvpOutboxDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<SprintHub> _hub;
    private readonly ILogger<PvpOutboxDispatcherService> _logger;
    private readonly string? _deploymentSlot;
    private readonly string? _activeSlotFile;
    private readonly string _workerId = $"pvp-outbox-{Environment.MachineName}-{Guid.NewGuid():N}";
    public PvpOutboxDispatcherService(
        IServiceScopeFactory scopeFactory,
        IHubContext<SprintHub> hub,
        ILogger<PvpOutboxDispatcherService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
        _deploymentSlot = configuration["Deployment:Slot"];
        _activeSlotFile = configuration["Deployment:ActiveSlotFile"];
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (!IsActivePublisher()) continue;
                await DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogError(ex, "PvP outbox dispatch failed."); }
        }
    }

    private bool IsActivePublisher()
    {
        if (string.IsNullOrWhiteSpace(_deploymentSlot) && string.IsNullOrWhiteSpace(_activeSlotFile))
            return true;
        if (string.IsNullOrWhiteSpace(_deploymentSlot) || string.IsNullOrWhiteSpace(_activeSlotFile))
        {
            _logger.LogError("PvP outbox publisher is disabled because Deployment:Slot and Deployment:ActiveSlotFile must be configured together.");
            return false;
        }
        try
        {
            var activeSlot = File.ReadAllText(_activeSlotFile).Trim();
            return string.Equals(activeSlot, _deploymentSlot, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PvP outbox publisher cannot read active slot file {ActiveSlotFile}; failing closed.", _activeSlotFile);
            return false;
        }
    }
    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WalkamonContext>();
        var now = DateTime.UtcNow;
        var leaseUntilValue = now.AddSeconds(30);
        var leaseUntil = new DateTime(
            leaseUntilValue.Ticks - leaseUntilValue.Ticks % TimeSpan.TicksPerMillisecond,
            DateTimeKind.Utc);
        await context.Database.ExecuteSqlInterpolatedAsync($@"
;WITH claim AS
(
    SELECT TOP (50) *
    FROM dbo.outbox_events WITH (UPDLOCK, READPAST, REPEATABLEREAD, ROWLOCK)
    WHERE published_at IS NULL
      AND (lease_until IS NULL OR lease_until < {now})
    ORDER BY created_at
)
UPDATE claim
SET lease_owner = {_workerId},
    lease_until = {leaseUntil},
    attempts = attempts + 1;", cancellationToken);
        var events = await context.OutboxEvents
            .Where(x => x.PublishedAt == null && x.LeaseOwner == _workerId && x.LeaseUntil == leaseUntil)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        foreach (var item in events)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(item.PayloadJson);
                var envelope = new
                {
                    eventId = item.EventId,
                    eventType = item.EventType,
                    aggregateId = item.AggregateId,
                    payload
                };
                if (item.AggregateType == "match") await _hub.Clients.Group($"match:{item.AggregateId}").SendAsync(item.EventType, envelope, cancellationToken);
                else if (item.AggregateType == "user") await _hub.Clients.Group($"user:{item.AggregateId}").SendAsync(item.EventType, envelope, cancellationToken);
                item.PublishedAt = DateTime.UtcNow; item.LeaseUntil = null; item.LeaseOwner = null;
            }
            catch { item.LeaseUntil = DateTime.UtcNow.AddSeconds(10); item.LeaseOwner = null; }
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
