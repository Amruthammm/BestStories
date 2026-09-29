using BestStories.Api.Models;
using BestStories.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BestStories.Api.Tests;

public class StoryCacheTests
{
    private static StoryCache CreateCache(FakeHackerNewsClient client, int maxParallelRequests = 5)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HackerNews:MaxParallelRequests"] = maxParallelRequests.ToString()
            })
            .Build();

        return new StoryCache(client, config, NullLogger<StoryCache>.Instance);
    }

    private static async Task<List<Story>> LoadAndGet(StoryCache cache, int count)
    {
        await cache.RefreshAsync(CancellationToken.None);
        return cache.GetBestStories(count)!;
    }

    [Fact]
    public async Task Returns_best_n_stories_ordered_by_score()
    {
        var client = new FakeHackerNewsClient();
        client.AddStory(1, score: 10);
        client.AddStory(2, score: 300);
        client.AddStory(3, score: 50);
        client.AddStory(4, score: 200);
        var cache = CreateCache(client);

        var stories = await LoadAndGet(cache, 3);

        Assert.Equal(new[] { 300, 200, 50 }, stories.Select(s => s.Score));
    }

    [Fact]
    public async Task Maps_hacker_news_fields_to_response()
    {
        var client = new FakeHackerNewsClient();
        client.AddStory(1, score: 1716);
        var cache = CreateCache(client);

        var story = (await LoadAndGet(cache, 1)).Single();

        Assert.Equal("Story 1", story.Title);
        Assert.Equal("https://example.com", story.Uri);
        Assert.Equal("user1", story.PostedBy);
        Assert.Equal(DateTimeOffset.Parse("2019-10-12T13:43:01+00:00"), story.Time);
        Assert.Equal(1716, story.Score);
        Assert.Equal(5, story.CommentCount);
    }

    [Fact]
    public async Task Story_without_url_links_to_hacker_news_page()
    {
        var client = new FakeHackerNewsClient();
        client.AddStory(42, score: 10, url: null);
        var cache = CreateCache(client);

        var story = (await LoadAndGet(cache, 1)).Single();

        Assert.Equal("https://news.ycombinator.com/item?id=42", story.Uri);
    }

    [Fact]
    public async Task Skips_deleted_dead_and_non_story_items()
    {
        var client = new FakeHackerNewsClient();
        client.AddStory(1, score: 10);
        client.AddItem(new HackerNewsItem { Id = 2, Type = "story", Title = "dead", Score = 999, Dead = true });
        client.AddItem(new HackerNewsItem { Id = 3, Type = "story", Title = "deleted", Score = 999, Deleted = true });
        client.AddItem(new HackerNewsItem { Id = 4, Type = "job", Title = "job", Score = 999 });
        var cache = CreateCache(client);

        var stories = await LoadAndGet(cache, 10);

        Assert.Single(stories);
        Assert.Equal("Story 1", stories[0].Title);
    }

    [Fact]
    public void Returns_null_before_the_first_load()
    {
        var client = new FakeHackerNewsClient();
        client.AddStory(1, score: 10);
        var cache = CreateCache(client);

        Assert.Null(cache.GetBestStories(10));
        Assert.Equal(0, client.IdCalls);
    }

    [Fact]
    public async Task Reading_stories_never_calls_hacker_news()
    {
        var client = new FakeHackerNewsClient();
        for (var id = 1; id <= 50; id++)
        {
            client.AddStory(id, score: id);
        }
        var cache = CreateCache(client);
        await cache.RefreshAsync(CancellationToken.None);

        // 1000 requests at the same time
        var requests = Enumerable.Range(0, 1000)
            .Select(_ => Task.Run(() => cache.GetBestStories(10)));
        var results = await Task.WhenAll(requests);

        Assert.All(results, r => Assert.Equal(10, r!.Count));
        // only the one refresh above called Hacker News
        Assert.Equal(1, client.IdCalls);
        Assert.Equal(50, client.ItemCalls);
    }

    [Fact]
    public async Task Does_not_call_hacker_news_more_than_the_parallel_limit()
    {
        var client = new FakeHackerNewsClient();
        for (var id = 1; id <= 100; id++)
        {
            client.AddStory(id, score: id);
        }
        var cache = CreateCache(client, maxParallelRequests: 3);

        await cache.RefreshAsync(CancellationToken.None);

        Assert.True(client.MaxRunningAtOnce <= 3, $"Max running at once was {client.MaxRunningAtOnce}");
    }

    [Fact]
    public async Task Keeps_old_stories_if_refresh_fails()
    {
        var client = new FakeHackerNewsClient();
        client.AddStory(1, score: 10);
        client.AddStory(2, score: 20);
        var cache = CreateCache(client);
        await cache.RefreshAsync(CancellationToken.None);

        client.IsDown = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => cache.RefreshAsync(CancellationToken.None));

        Assert.Equal(2, cache.GetBestStories(10)!.Count);
    }

    [Fact]
    public async Task Keeps_old_stories_if_every_story_fails_to_load()
    {
        var client = new FakeHackerNewsClient();
        client.AddStory(1, score: 10);
        client.AddStory(2, score: 20);
        var cache = CreateCache(client);
        await cache.RefreshAsync(CancellationToken.None);

        client.FailItems = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => cache.RefreshAsync(CancellationToken.None));

        Assert.Equal(2, cache.GetBestStories(10)!.Count);
    }
}
