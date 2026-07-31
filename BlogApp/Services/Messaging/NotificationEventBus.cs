using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BlogApp.Models;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BlogApp.Services.Messaging;

/// <summary>
/// Global RabbitMQ settings (appsettings.json / env). Empty HostName disables the broker.
/// </summary>
public class RabbitMqOptions
{
    /// <summary>Empty or whitespace = disable RabbitMQ (in-process Channel only).</summary>
    public string? HostName { get; set; }
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string FanoutExchange { get; set; } = "blog.notifications.fanout";
    public string TopicExchange { get; set; } = "blog.notifications.topic";
    public string ClientProvidedName { get; set; } = "blogapp-notifications";
    public bool AutomaticRecoveryEnabled { get; set; } = true;
    public int NetworkRecoverySeconds { get; set; } = 10;
    public int RequestedHeartbeatSeconds { get; set; } = 30;
    public int RequestedConnectionTimeoutSeconds { get; set; } = 5;

    public bool Enabled => !string.IsNullOrWhiteSpace(HostName);
}

/// <summary>
/// Domain event bus for notifications.
/// Always publishes to an in-process Channel (SSE consumers).
/// When RabbitMQ is configured, also publishes to fanout + topic exchanges
/// and consumes fanout so multi-instance deployments stay in sync.
/// </summary>
public interface INotificationEventBus
{
    ValueTask PublishAsync(NotificationDeliveredEvent evt, CancellationToken ct = default);
    ChannelReader<NotificationDeliveredEvent> Reader { get; }
}

public sealed class NotificationEventBus : INotificationEventBus, IAsyncDisposable
{
    private readonly Channel<NotificationDeliveredEvent> _channel =
        Channel.CreateUnbounded<NotificationDeliveredEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

    private readonly RabbitMqOptions _opts;
    private readonly ILogger<NotificationEventBus> _log;
    private IConnection? _conn;
    private IChannel? _pub;
    private IChannel? _sub;
    private string? _queueName;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _connectStarted;

    public NotificationEventBus(IOptions<RabbitMqOptions> opts, ILogger<NotificationEventBus> log)
    {
        _opts = opts.Value;
        _log = log;
        if (_opts.Enabled)
            _ = TryConnectAsync();
    }

    public ChannelReader<NotificationDeliveredEvent> Reader => _channel.Reader;

    public async ValueTask PublishAsync(NotificationDeliveredEvent evt, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(evt, ct);

        var pub = _pub;
        if (!_opts.Enabled || pub is null || !pub.IsOpen)
            return;

        try
        {
            var json = JsonSerializer.Serialize(evt);
            var body = Encoding.UTF8.GetBytes(json);
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = evt.NotificationId.ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await pub.BasicPublishAsync(
                exchange: _opts.FanoutExchange,
                routingKey: string.Empty,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);

            var rk = $"notif.{evt.Kind}";
            await pub.BasicPublishAsync(
                exchange: _opts.TopicExchange,
                routingKey: rk,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "RabbitMQ publish failed for NotificationId={Id}", evt.NotificationId);
        }
    }

    private async Task TryConnectAsync()
    {
        if (Interlocked.Exchange(ref _connectStarted, 1) == 1)
            return;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _opts.HostName!,
                Port = _opts.Port,
                UserName = _opts.UserName,
                Password = _opts.Password,
                VirtualHost = _opts.VirtualHost,
                AutomaticRecoveryEnabled = _opts.AutomaticRecoveryEnabled,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(Math.Max(1, _opts.NetworkRecoverySeconds)),
                RequestedHeartbeat = TimeSpan.FromSeconds(Math.Max(0, _opts.RequestedHeartbeatSeconds)),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(Math.Max(1, _opts.RequestedConnectionTimeoutSeconds))
            };

            var clientName = string.IsNullOrWhiteSpace(_opts.ClientProvidedName)
                ? "blogapp-notifications"
                : _opts.ClientProvidedName;

            _conn = await factory.CreateConnectionAsync(clientName);
            _pub = await _conn.CreateChannelAsync();
            _sub = await _conn.CreateChannelAsync();

            await _pub.ExchangeDeclareAsync(_opts.FanoutExchange, ExchangeType.Fanout, durable: true, autoDelete: false);
            await _pub.ExchangeDeclareAsync(_opts.TopicExchange, ExchangeType.Topic, durable: true, autoDelete: false);

            var q = await _sub.QueueDeclareAsync(
                queue: string.Empty,
                durable: false,
                exclusive: true,
                autoDelete: true);
            _queueName = q.QueueName;

            await _sub.QueueBindAsync(_queueName, _opts.FanoutExchange, routingKey: string.Empty);

            var consumer = new AsyncEventingBasicConsumer(_sub);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<NotificationDeliveredEvent>(json);
                    if (evt is not null)
                        await _channel.Writer.WriteAsync(evt);
                    await _sub.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "RabbitMQ consume failed");
                    try { await _sub.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false); } catch { /* ignore */ }
                }
            };
            await _sub.BasicConsumeAsync(_queueName, autoAck: false, consumer);

            _log.LogInformation("RabbitMQ notifications connected Host={Host} Fanout={Fanout} Topic={Topic}",
                _opts.HostName, _opts.FanoutExchange, _opts.TopicExchange);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "RabbitMQ unavailable — using in-process Channel only");
            try { if (_sub is not null) await _sub.DisposeAsync(); } catch { }
            try { if (_pub is not null) await _pub.DisposeAsync(); } catch { }
            try { if (_conn is not null) await _conn.DisposeAsync(); } catch { }
            _sub = null;
            _pub = null;
            _conn = null;
            Interlocked.Exchange(ref _connectStarted, 0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            try { if (_sub is not null) await _sub.CloseAsync(); } catch { }
            try { if (_pub is not null) await _pub.CloseAsync(); } catch { }
            try { if (_conn is not null) await _conn.CloseAsync(); } catch { }
            if (_sub is not null) await _sub.DisposeAsync();
            if (_pub is not null) await _pub.DisposeAsync();
            if (_conn is not null) await _conn.DisposeAsync();
        }
        finally
        {
            _gate.Release();
        }
    }
}
