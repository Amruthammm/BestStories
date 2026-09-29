namespace BestStories.Api.Services;

// Background job that reloads the best stories every X seconds (see appsettings.json).
// Because of this, the number of calls to Hacker News stays the same no matter
// how many requests our API gets.
public class StoryRefreshService : BackgroundService
{
    private readonly IStoryCache _cache;
    private readonly ILogger<StoryRefreshService> _logger;
    private readonly TimeSpan _interval;

    public StoryRefreshService(IStoryCache cache, IConfiguration config, ILogger<StoryRefreshService> logger)
    {
        _cache = cache;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(config.GetValue("HackerNews:RefreshIntervalSeconds", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _cache.RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Hacker News is down or slow - keep serving the stories we already have
                _logger.LogError(ex, "Failed to refresh best stories");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
