using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BlogApp.Models;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BlogApp.Services.Messaging;

public class RabbitMqOptions
{
    /// <summary>Empty = disable RabbitMQ (Channel-only local bus).</summary>
    public string? HostName { get; set; }
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string FanoutExchange { get; set; } = "blog.notifications.fanout";
    public string TopicExchange { get; set; } = "blog.notifications.topic";
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
    private IModel? _pub;
    private IModel? _sub;
    private string? _queueName;
    private readonly object _gate = new();

    public NotificationEventBus(IOptions<RabbitMqOptions> opts, ILogger<NotificationEventBus> log)
    {
        _opts = opts.Value;
        _log = log;
        if (_opts.Enabled)
            TryConnect();
    }

    public ChannelReader<NotificationDeliveredEvent> Reader => _channel.Reader;

    public async ValueTask PublishAsync(NotificationDeliveredEvent evt, CancellationToken ct = default)
    {
        // Local channel always (same process SSE)
        await _channel.Writer.WriteAsync(evt, ct);

        if (!_opts.Enabled || _pub is null || !_pub.IsOpen)
            return;

        try
        {
            var json = JsonSerializer.Serialize(evt);
            var body = Encoding.UTF8.GetBytes(json);
            var props = _pub.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2; // persistent
            props.MessageId = evt.NotificationId.ToString();
            props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Fanout → every consumer instance
            _pub.BasicPublish(_opts.FanoutExchange, routingKey: "", basicProperties: props, body: body);

            // Topic → routing by kind (e.g. notif.NewPost, notif.Broadcast)
            var rk = $"notif.{evt.Kind}";
            _pub.BasicPublish(_opts.TopicExchange, routingKey: rk, basicProperties: props, body: body);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "RabbitMQ publish failed for NotificationId={Id}", evt.NotificationId);
        }
    }

    private void TryConnect()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _opts.HostName!,
                Port = _opts.Port,
                UserName = _opts.UserName,
                Password = _opts.Password,
                VirtualHost = _opts.VirtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _conn = factory.CreateConnection("blogapp-notifications");
            _pub = _conn.CreateModel();
            _sub = _conn.CreateModel();

            _pub.ExchangeDeclare(_opts.FanoutExchange, ExchangeType.Fanout, durable: true, autoDelete: false);
            _pub.ExchangeDeclare(_opts.TopicExchange, ExchangeType.Topic, durable: true, autoDelete: false);

            // Exclusive auto-delete queue per instance for fanout fan-in
            _queueName = _sub.QueueDeclare(
                queue: "",
                durable: false,
                exclusive: true,
                autoDelete: true).QueueName;

            _sub.QueueBind(_queueName, _opts.FanoutExchange, routingKey: "");

            var consumer = new AsyncEventingBasicConsumer(_sub);
            consumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<NotificationDeliveredEvent>(json);
                    if (evt is not null)
                        await _channel.Writer.WriteAsync(evt);
                    _sub.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "RabbitMQ consume failed");
                    try { _sub.BasicNack(ea.DeliveryTag, false, requeue: false); } catch { /* ignore */ }
                }
            };
            _sub.BasicConsume(_queueName, autoAck: false, consumer);

            _log.LogInformation("RabbitMQ notifications connected Host={Host} Fanout={Fanout} Topic={Topic}",
                _opts.HostName, _opts.FanoutExchange, _opts.TopicExchange);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "RabbitMQ unavailable — using in-process Channel only");
            try { _sub?.Dispose(); } catch { }
            try { _pub?.Dispose(); } catch { }
            try { _conn?.Dispose(); } catch { }
            _sub = null;
            _pub = null;
            _conn = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            try { _sub?.Close(); } catch { }
            try { _pub?.Close(); } catch { }
            try { _conn?.Close(); } catch { }
            _sub?.Dispose();
            _pub?.Dispose();
            _conn?.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}
