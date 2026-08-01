using BLL.Interfaces;
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
    private readonly IHubContext<PresenceHub> _presenceHub;
    private readonly IPvpPresenceTracker _presenceTracker;
    private readonly ILogger<PvpOutboxDispatcherService> _logger;
    private readonly string? _deploymentSlot;
    private readonly string? _activeSlotFile;
    private readonly string _workerId = $"pvp-outbox-{Environment.MachineName}-{Guid.NewGuid():N}";
    public PvpOutboxDispatcherService(
        IServiceScopeFactory scopeFactory,
        IHubContext<SprintHub> hub,
        IHubContext<PresenceHub> presenceHub,
        IPvpPresenceTracker presenceTracker,
        ILogger<PvpOutboxDispatcherService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _presenceHub = presenceHub;
        _presenceTracker = presenceTracker;
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
                if (item.AggregateType == "presence")
                {
                    var friendIds = await context.Friendships.AsNoTracking()
                        .Where(x => x.UserLowId == item.AggregateId || x.UserHighId == item.AggregateId)
                        .Select(x => x.UserLowId == item.AggregateId ? x.UserHighId : x.UserLowId)
                        .ToListAsync(cancellationToken);
                    var isOnline = _presenceTracker.IsOnline(item.AggregateId);
                    var isBusy = isOnline && await context.PvpPlayerActivities.AsNoTracking()
                        .AnyAsync(x => x.UserId == item.AggregateId, cancellationToken);
                    var presencePayload = new
                    {
                        userId = item.AggregateId,
                        isOnline,
                        pvpAvailabilityCode = !isOnline ? "offline" : isBusy ? "busy" : "available",
                        serverTime = DateTime.UtcNow
                    };
                    var presenceEnvelope = new
                    {
                        eventId = item.EventId,
                        eventType = item.EventType,
                        aggregateId = item.AggregateId,
                        payload = presencePayload
                    };
                    if (friendIds.Count > 0)
                    {
                        var groups = friendIds.Select(x => $"user:{x}").ToList();
                        await Task.WhenAll(
                            _hub.Clients.Groups(groups)
                                .SendAsync(item.EventType, presenceEnvelope, cancellationToken),
                            _presenceHub.Clients.Groups(groups)
                                .SendAsync(item.EventType, presenceEnvelope, cancellationToken));
                    }
                }
                else
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
                    else if (item.AggregateType == "user")
                    {
                        var group = $"user:{item.AggregateId}";
                        await Task.WhenAll(
                            _hub.Clients.Group(group)
                                .SendAsync(item.EventType, envelope, cancellationToken),
                            _presenceHub.Clients.Group(group)
                                .SendAsync(item.EventType, envelope, cancellationToken));
                    }
                }
                item.PublishedAt = DateTime.UtcNow; item.LeaseUntil = null; item.LeaseOwner = null;
            }
            catch { item.LeaseUntil = DateTime.UtcNow.AddSeconds(10); item.LeaseOwner = null; }
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
