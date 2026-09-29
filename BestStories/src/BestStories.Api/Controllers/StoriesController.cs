using BestStories.Api.Models;
using BestStories.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BestStories.Api.Controllers;

[ApiController]
[Route("api/stories")]
public class StoriesController : ControllerBase
{
    private readonly IStoryCache _storyCache;

    public StoriesController(IStoryCache storyCache)
    {
        _storyCache = storyCache;
    }

    /// <summary>
    /// Returns the best n stories from Hacker News, highest score first.
    /// </summary>
    /// <param name="n">Number of stories to return. If n is more than the stories available, all of them are returned.</param>
    [HttpGet("best")]
    [ProducesResponseType(typeof(List<Story>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetBestStories([FromQuery] int n)
    {
        if (n < 1)
        {
            return BadRequest("n must be greater than 0");
        }

        var stories = _storyCache.GetBestStories(n);

        if (stories == null)
        {
            // The background refresh hasn't finished its first load yet (only right after startup)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Stories are still loading, please try again in a few seconds");
        }

        return Ok(stories);
    }
}
