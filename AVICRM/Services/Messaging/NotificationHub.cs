using System.Collections.Concurrent;
using System.Text.Json;
using AVICRM.Models;

namespace AVICRM.Services.Messaging;

/// <summary>Per-user SSE fan-out for in-app notifications.</summary>
public sealed class NotificationHub
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, ChannelWriterBox>> _subs = new();

    public (Guid Id, System.Threading.Channels.ChannelReader<string> Reader) Subscribe(string userId)
    {
        var id = Guid.NewGuid();
        var ch = System.Threading.Channels.Channel.CreateUnbounded<string>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var bag = _subs.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, ChannelWriterBox>());
        bag[id] = new ChannelWriterBox(ch.Writer);
        return (id, ch.Reader);
    }

    public void Unsubscribe(string userId, Guid id)
    {
        if (_subs.TryGetValue(userId, out var bag))
        {
            bag.TryRemove(id, out _);
            if (bag.IsEmpty)
                _subs.TryRemove(userId, out _);
        }
    }

    public void PushToUser(string userId, NotificationDeliveredEvent evt)
    {
        if (!_subs.TryGetValue(userId, out var bag) || bag.IsEmpty) return;
        var json = JsonSerializer.Serialize(new
        {
            type = "notification",
            id = evt.NotificationId,
            kind = evt.Kind.ToString(),
            title = evt.Title,
            body = evt.Body,
            linkUrl = evt.LinkUrl,
            createdAtUtc = evt.CreatedAtUtc
        });
        foreach (var w in bag.Values)
            w.TryWrite(json);
    }

    private sealed class ChannelWriterBox
    {
        private readonly System.Threading.Channels.ChannelWriter<string> _w;
        public ChannelWriterBox(System.Threading.Channels.ChannelWriter<string> w) => _w = w;
        public void TryWrite(string msg) => _w.TryWrite(msg);
    }
}

/// <summary>Reads NotificationEventBus and pushes to SSE subscribers.</summary>
public sealed class NotificationRealtimeHostedService : BackgroundService
{
    private readonly INotificationEventBus _bus;
    private readonly NotificationHub _hub;
    private readonly ILogger<NotificationRealtimeHostedService> _log;

    public NotificationRealtimeHostedService(
        INotificationEventBus bus,
        NotificationHub hub,
        ILogger<NotificationRealtimeHostedService> log)
    {
        _bus = bus;
        _hub = hub;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Notification realtime SSE consumer started");
        await foreach (var evt in _bus.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _hub.PushToUser(evt.UserId, evt);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SSE push failed UserId={UserId}", evt.UserId);
            }
        }
    }
}

/// <summary>Processes scheduled NotificationCampaign rows.</summary>
public sealed class NotificationSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationSchedulerHostedService> _log;

    public NotificationSchedulerHostedService(
        IServiceScopeFactory scopes,
        ILogger<NotificationSchedulerHostedService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                var n = await dispatcher.ProcessDueCampaignsAsync(stoppingToken);
                if (n > 0)
                    _log.LogInformation("Processed {Count} scheduled notification campaigns", n);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Notification scheduler tick failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
