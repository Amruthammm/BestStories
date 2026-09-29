using System.Collections.Concurrent;
using BestStories.Api.Models;

namespace BestStories.Api.Services;

public interface IStoryCache
{
    // Returns null if the stories haven't been loaded yet
    List<Story>? GetBestStories(int count);
    Task RefreshAsync(CancellationToken cancellationToken);
}

// Keeps the best stories in memory, already sorted by score.
// API requests only read from here and never call Hacker News.
// Only StoryRefreshService calls RefreshAsync, so there is only ever one refresh running.
public class StoryCache : IStoryCache
{
    private readonly IHackerNewsClient _client;
    private readonly ILogger<StoryCache> _logger;
    private readonly int _maxParallelRequests;

    // Replaced with a new list on every refresh, never modified in place,
    // so requests can read it safely while a refresh is running.
    private volatile List<Story>? _stories;

    public StoryCache(IHackerNewsClient client, IConfiguration config, ILogger<StoryCache> logger)
    {
        _client = client;
        _logger = logger;
        _maxParallelRequests = config.GetValue("HackerNews:MaxParallelRequests", 5);
    }

    public List<Story>? GetBestStories(int count)
    {
        return _stories?.Take(count).ToList();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // If this throws (Hacker News down), _stories keeps the old list
        _stories = await LoadStoriesAsync(cancellationToken);
    }

    private async Task<List<Story>> LoadStoriesAsync(CancellationToken cancellationToken)
    {
        var ids = await _client.GetBestStoryIdsAsync(cancellationToken);
        var stories = new ConcurrentBag<Story>();
        var failed = 0;

        // Fetch the stories in parallel, but only a few at a time so we don't flood Hacker News
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxParallelRequests,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(ids, options, async (id, ct) =>
        {
            try
            {
                var item = await _client.GetItemAsync(id, ct);
                if (item != null && item.Type == "story" && !item.Deleted && !item.Dead)
                {
                    stories.Add(Story.FromItem(item));
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // skip this one story rather than failing the whole refresh
                Interlocked.Increment(ref failed);
                _logger.LogWarning(ex, "Could not load story {Id}", id);
            }
        });

        if (failed > 0 && stories.IsEmpty)
        {
            // every story failed - throw so we keep the old list instead of replacing it with an empty one
            throw new HttpRequestException("Could not load any stories from Hacker News");
        }

        _logger.LogInformation("Loaded {Count} best stories", stories.Count);

        return stories.OrderByDescending(s => s.Score).ToList();
    }
}
