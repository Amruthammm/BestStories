using BestStories.Api.Models;
using BestStories.Api.Services;

namespace BestStories.Api.Tests;

// Fake Hacker News so the tests don't need the internet.
// It also counts calls so we can check how often Hacker News is hit.
public class FakeHackerNewsClient : IHackerNewsClient
{
    private readonly Dictionary<long, HackerNewsItem> _items = new();
    private int _running;

    public List<long> BestIds { get; } = new();
    public bool IsDown { get; set; }
    public bool FailItems { get; set; }
    public int IdCalls;
    public int ItemCalls;
    public int MaxRunningAtOnce;

    public void AddStory(long id, int score, string? url = "https://example.com")
    {
        BestIds.Add(id);
        _items[id] = new HackerNewsItem
        {
            Id = id,
            Type = "story",
            Title = "Story " + id,
            Url = url,
            By = "user" + id,
            Time = 1570887781,
            Score = score,
            Descendants = 5
        };
    }

    public void AddItem(HackerNewsItem item)
    {
        BestIds.Add(item.Id);
        _items[item.Id] = item;
    }

    public async Task<List<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref IdCalls);
        await Task.Delay(10, cancellationToken);

        if (IsDown)
        {
            throw new HttpRequestException("Hacker News is down");
        }

        return BestIds.ToList();
    }

    public async Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ItemCalls);
        var running = Interlocked.Increment(ref _running);
        lock (_items)
        {
            MaxRunningAtOnce = Math.Max(MaxRunningAtOnce, running);
        }

        await Task.Delay(5, cancellationToken);
        Interlocked.Decrement(ref _running);

        if (FailItems)
        {
            throw new HttpRequestException("Story failed");
        }

        lock (_items)
        {
            return _items.GetValueOrDefault(id);
        }
    }
}
