using System.Threading.Channels;
using BlogApp.Models;

namespace BlogApp.Services.Seo;

/// <summary>
/// Bounded in-memory queue. Middleware enqueues; hosted service drains to SQLite.
/// Drop-newest on full so crawl spikes never block the request pipeline.
/// </summary>
public sealed class BotCrawlLogQueue
{
    private readonly Channel<BotCrawlHit> _channel;

    public BotCrawlLogQueue()
    {
        _channel = Channel.CreateBounded<BotCrawlHit>(new BoundedChannelOptions(4000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool TryEnqueue(BotCrawlHit hit) => _channel.Writer.TryWrite(hit);

    public ChannelReader<BotCrawlHit> Reader => _channel.Reader;
}
