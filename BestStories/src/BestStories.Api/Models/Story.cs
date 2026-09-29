namespace BestStories.Api.Models;

// What our API returns
public class Story
{
    public string Title { get; set; } = "";
    public string Uri { get; set; } = "";
    public string PostedBy { get; set; } = "";
    public DateTimeOffset Time { get; set; }
    public int Score { get; set; }
    public int CommentCount { get; set; }

    public static Story FromItem(HackerNewsItem item)
    {
        return new Story
        {
            Title = item.Title ?? "",
            // Ask HN posts don't have a url, so link to the HN discussion page instead
            Uri = string.IsNullOrEmpty(item.Url) ? $"https://news.ycombinator.com/item?id={item.Id}" : item.Url,
            PostedBy = item.By ?? "",
            Time = DateTimeOffset.FromUnixTimeSeconds(item.Time),
            Score = item.Score,
            CommentCount = item.Descendants ?? 0
        };
    }
}
