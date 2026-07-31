using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BlogApp.Services.Messaging;

/// <summary>
/// High-throughput, resilient API work pipeline:
/// - Publish requests to a RabbitMQ topic exchange (or in-process channel).
/// - Consume with prefetch=1 so work is handled one-by-one (no stampede).
/// - Persistent messages + manual ack → no silent loss on crash.
/// - RPC-style correlation for HTTP waiters (timeout → 503).
/// </summary>
public interface IApiTopicBus
{
    /// <summary>Enqueue work and wait for the sequential worker result.</summary>
    Task<ApiWorkResult> EnqueueAndWaitAsync(ApiWorkRequest request, TimeSpan? timeout = null, CancellationToken ct = default);

    /// <summary>Fire-and-forget (webhooks, analytics side-effects). Always durable when RMQ is on.</summary>
    ValueTask PublishFireAndForgetAsync(ApiWorkRequest request, CancellationToken ct = default);
}

public sealed class ApiWorkRequest
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Routing key segment, e.g. posts.list | comments.create | webhooks.create</summary>
    public string Kind { get; set; } = "generic";
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string? UserId { get; set; }
    public int? ApiKeyId { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime EnqueuedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ApiWorkResult
{
    public string CorrelationId { get; set; } = "";
    public bool Ok { get; set; }
    public int StatusCode { get; set; } = 200;
    public string? BodyJson { get; set; }
    public string? Error { get; set; }
}

public delegate Task<ApiWorkResult> ApiWorkHandler(ApiWorkRequest request, CancellationToken ct);

public sealed class ApiTopicBusOptions
{
    /// <summary>When true and RabbitMQ host is set, use durable topic + queue.</summary>
    public bool UseRabbit { get; set; } = true;
    public string TopicExchange { get; set; } = "blog.api.topic";
    public string WorkQueue { get; set; } = "blog.api.work";
    public string RoutingPrefix { get; set; } = "api";
    /// <summary>Prefetch=1 → process one message at a time per consumer.</summary>
    public ushort Prefetch { get; set; } = 1;
    public int DefaultTimeoutSeconds { get; set; } = 25;
    public int InProcessCapacity { get; set; } = 10_000;
}

public sealed class ApiTopicBus : IApiTopicBus, IHostedService, IAsyncDisposable
{
    private readonly RabbitMqOptions _rmq;
    private readonly ApiTopicBusOptions _opts;
    private readonly ILogger<ApiTopicBus> _log;
    private readonly IServiceScopeFactory _scopes;

    private readonly Channel<ApiWorkRequest> _local =
        Channel.CreateBounded<ApiWorkRequest>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ApiWorkResult>> _waiters = new();

    private IConnection? _conn;
    private IChannel? _pub;
    private IChannel? _sub;
    private CancellationTokenSource? _cts;
    private Task? _localWorker;
    private bool _rabbitReady;

    public ApiTopicBus(
        IOptions<RabbitMqOptions> rmq,
        IOptions<ApiTopicBusOptions> opts,
        ILogger<ApiTopicBus> log,
        IServiceScopeFactory scopes)
    {
        _rmq = rmq.Value;
        _opts = opts.Value;
        _log = log;
        _scopes = scopes;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _localWorker = Task.Run(() => RunLocalWorkerAsync(_cts.Token), _cts.Token);

        if (_opts.UseRabbit && _rmq.Enabled)
            _ = TryConnectRabbitAsync();

        _log.LogInformation(
            "ApiTopicBus started Rabbit={Rabbit} Exchange={Ex} Queue={Q} Prefetch={P}",
            _rabbitReady, _opts.TopicExchange, _opts.WorkQueue, _opts.Prefetch);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try { _cts?.Cancel(); } catch { }
        if (_localWorker is not null)
            try { await _localWorker.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); } catch { }
        await DisposeAsync();
    }

    public async Task<ApiWorkResult> EnqueueAndWaitAsync(
        ApiWorkRequest request,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<ApiWorkResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiters[request.CorrelationId] = tcs;

        try
        {
            await PublishCoreAsync(request, ct);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(timeout ?? TimeSpan.FromSeconds(_opts.DefaultTimeoutSeconds));

            await using (linked.Token.Register(() =>
                         tcs.TrySetResult(new ApiWorkResult
                         {
                             CorrelationId = request.CorrelationId,
                             Ok = false,
                             StatusCode = 503,
                             Error = "work_timeout"
                         })))
            {
                return await tcs.Task;
            }
        }
        finally
        {
            _waiters.TryRemove(request.CorrelationId, out _);
        }
    }

    public ValueTask PublishFireAndForgetAsync(ApiWorkRequest request, CancellationToken ct = default)
        => new(PublishCoreAsync(request, ct));

    private async Task PublishCoreAsync(ApiWorkRequest request, CancellationToken ct)
    {
        var pub = _pub;
        if (_rabbitReady && pub is { IsOpen: true })
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
                var props = new BasicProperties
                {
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    MessageId = request.CorrelationId,
                    CorrelationId = request.CorrelationId,
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    Headers = new Dictionary<string, object?>
                    {
                        ["x-kind"] = request.Kind,
                        ["x-path"] = request.Path
                    }
                };

                var rk = $"{_opts.RoutingPrefix}.{request.Kind}";
                await pub.BasicPublishAsync(
                    exchange: _opts.TopicExchange,
                    routingKey: rk,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: ct);
                return;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Rabbit publish failed — falling back to local channel");
            }
        }

        await _local.Writer.WriteAsync(request, ct);
    }

    private async Task RunLocalWorkerAsync(CancellationToken ct)
    {
        await foreach (var req in _local.Reader.ReadAllAsync(ct))
        {
            try
            {
                var result = await ExecuteAsync(req, ct);
                CompleteWaiter(result);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Local API work failed CorrelationId={Id}", req.CorrelationId);
                CompleteWaiter(new ApiWorkResult
                {
                    CorrelationId = req.CorrelationId,
                    Ok = false,
                    StatusCode = 500,
                    Error = "work_failed"
                });
            }
        }
    }

    private async Task TryConnectRabbitAsync()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _rmq.HostName!,
                Port = _rmq.Port,
                UserName = _rmq.UserName,
                Password = _rmq.Password,
                VirtualHost = _rmq.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _conn = await factory.CreateConnectionAsync("blogapp-api-work");
            _pub = await _conn.CreateChannelAsync();
            _sub = await _conn.CreateChannelAsync();

            await _pub.ExchangeDeclareAsync(_opts.TopicExchange, ExchangeType.Topic, durable: true, autoDelete: false);

            await _sub.QueueDeclareAsync(
                queue: _opts.WorkQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-queue-type"] = "classic"
                });

            await _sub.QueueBindAsync(_opts.WorkQueue, _opts.TopicExchange, routingKey: _opts.RoutingPrefix + ".#");

            await _sub.BasicQosAsync(0, _opts.Prefetch, global: false);

            var consumer = new AsyncEventingBasicConsumer(_sub);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                ApiWorkRequest? req = null;
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    req = JsonSerializer.Deserialize<ApiWorkRequest>(json);
                    if (req is null)
                    {
                        await _sub.BasicAckAsync(ea.DeliveryTag, multiple: false);
                        return;
                    }

                    var result = await ExecuteAsync(req, _cts?.Token ?? CancellationToken.None);
                    CompleteWaiter(result);
                    await _sub.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Rabbit work failed CorrelationId={Id}", req?.CorrelationId);
                    if (req is not null)
                    {
                        CompleteWaiter(new ApiWorkResult
                        {
                            CorrelationId = req.CorrelationId,
                            Ok = false,
                            StatusCode = 500,
                            Error = "work_failed"
                        });
                    }
                    try { await _sub.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false); } catch { }
                }
            };

            await _sub.BasicConsumeAsync(_opts.WorkQueue, autoAck: false, consumer);
            _rabbitReady = true;
            _log.LogInformation("ApiTopicBus Rabbit connected Host={Host} Queue={Q}", _rmq.HostName, _opts.WorkQueue);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "ApiTopicBus Rabbit unavailable — in-process sequential channel only");
            _rabbitReady = false;
            try { if (_sub is not null) await _sub.DisposeAsync(); } catch { }
            try { if (_pub is not null) await _pub.DisposeAsync(); } catch { }
            try { if (_conn is not null) await _conn.DisposeAsync(); } catch { }
            _sub = null;
            _pub = null;
            _conn = null;
        }
    }

    private async Task<ApiWorkResult> ExecuteAsync(ApiWorkRequest req, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<ApiWorkHandlerRegistry>();
        return await registry.DispatchAsync(req, ct);
    }

    private void CompleteWaiter(ApiWorkResult result)
    {
        if (_waiters.TryRemove(result.CorrelationId, out var tcs))
            tcs.TrySetResult(result);
    }

    public async ValueTask DisposeAsync()
    {
        try { if (_sub is not null) await _sub.CloseAsync(); } catch { }
        try { if (_pub is not null) await _pub.CloseAsync(); } catch { }
        try { if (_conn is not null) await _conn.CloseAsync(); } catch { }
        if (_sub is not null) await _sub.DisposeAsync();
        if (_pub is not null) await _pub.DisposeAsync();
        if (_conn is not null) await _conn.DisposeAsync();
    }
}

/// <summary>Maps Kind → handler. Register handlers in DI.</summary>
public sealed class ApiWorkHandlerRegistry
{
    private readonly Dictionary<string, ApiWorkHandler> _map;
    private readonly ILogger<ApiWorkHandlerRegistry> _log;

    public ApiWorkHandlerRegistry(IEnumerable<IApiWorkHandler> handlers, ILogger<ApiWorkHandlerRegistry> log)
    {
        _log = log;
        _map = new Dictionary<string, ApiWorkHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in handlers)
        {
            foreach (var kind in h.Kinds)
                _map[kind] = h.HandleAsync;
        }
    }

    public async Task<ApiWorkResult> DispatchAsync(ApiWorkRequest req, CancellationToken ct)
    {
        if (_map.TryGetValue(req.Kind, out var handler))
            return await handler(req, ct);

        if (string.Equals(req.Kind, "passthrough", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiWorkResult
            {
                CorrelationId = req.CorrelationId,
                Ok = true,
                StatusCode = 200,
                BodyJson = req.PayloadJson
            };
        }

        _log.LogWarning("No handler for API work kind={Kind}", req.Kind);
        return new ApiWorkResult
        {
            CorrelationId = req.CorrelationId,
            Ok = false,
            StatusCode = 501,
            Error = "no_handler"
        };
    }
}

public interface IApiWorkHandler
{
    IEnumerable<string> Kinds { get; }
    Task<ApiWorkResult> HandleAsync(ApiWorkRequest request, CancellationToken ct);
}
