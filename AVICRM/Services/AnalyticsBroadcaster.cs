using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace AVICRM.Services;

/// <summary>
/// A tiny in-process pub/sub for pushing live dashboard events (new view, new comment,
/// comment status change) to every connected admin browser tab over Server-Sent Events.
/// One instance per running process — fine for a single-container deployment; if you ever
/// scale this app to multiple instances behind a load balancer, this would need to move to
/// a shared backplane (Redis pub/sub, etc.) so events reach clients connected to a
/// different instance.
/// </summary>
public class AnalyticsBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public (Guid Id, ChannelReader<string> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id) => _subscribers.TryRemove(id, out _);

    public void Publish(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(json);
    }
}
