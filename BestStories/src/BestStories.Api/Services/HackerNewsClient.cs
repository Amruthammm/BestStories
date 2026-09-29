using BestStories.Api.Models;

namespace BestStories.Api.Services;

public interface IHackerNewsClient
{
    Task<List<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken);
    Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken);
}

public class HackerNewsClient : IHackerNewsClient
{
    private readonly HttpClient _httpClient;

    public HackerNewsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await _httpClient.GetFromJsonAsync<List<long>>("beststories.json", cancellationToken);
        return ids ?? new List<long>();
    }

    public Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken)
    {
        return _httpClient.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", cancellationToken);
    }
}
