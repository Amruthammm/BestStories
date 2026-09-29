using BestStories.Api.Controllers;
using BestStories.Api.Models;
using BestStories.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BestStories.Api.Tests;

public class StoriesControllerTests
{
    private static async Task<StoriesController> CreateController(FakeHackerNewsClient client, bool loadStories = true)
    {
        var config = new ConfigurationBuilder().Build();
        var cache = new StoryCache(client, config, NullLogger<StoryCache>.Instance);
        if (loadStories)
        {
            await cache.RefreshAsync(CancellationToken.None);
        }

        return new StoriesController(cache);
    }

    private static FakeHackerNewsClient TwoStories()
    {
        var client = new FakeHackerNewsClient();
        client.AddStory(1, score: 10);
        client.AddStory(2, score: 20);
        return client;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Returns_bad_request_for_invalid_n(int n)
    {
        var controller = await CreateController(TwoStories());

        var result = controller.GetBestStories(n);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Returns_ok_with_stories()
    {
        var controller = await CreateController(TwoStories());

        var result = controller.GetBestStories(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var stories = Assert.IsType<List<Story>>(ok.Value);
        Assert.Equal(20, stories.Single().Score);
    }

    [Fact]
    public async Task Returns_all_stories_when_n_is_bigger_than_available()
    {
        var controller = await CreateController(TwoStories());

        var result = controller.GetBestStories(1000);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, Assert.IsType<List<Story>>(ok.Value).Count);
    }

    [Fact]
    public async Task Returns_503_when_stories_are_not_loaded_yet()
    {
        var client = TwoStories();
        var controller = await CreateController(client, loadStories: false);

        var result = controller.GetBestStories(5);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
        Assert.Equal(0, client.IdCalls); // the request did not call Hacker News
    }
}
