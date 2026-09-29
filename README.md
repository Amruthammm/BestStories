# Hacker News Best Stories API

ASP.NET Core Web API (.NET 8) that returns the best `n` stories from Hacker News, sorted by score.

## How to run

You need the .NET 8 SDK.

```
dotnet run --project src/BestStories.Api
```

Swagger opens at http://localhost:5000/swagger. You can also call the API directly:

```
GET http://localhost:5000/api/stories/best?n=10
```

To run the tests:

```
dotnet test
```

## Example response

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

- `400` if `n` is less than 1
- `503` if the stories haven't been loaded yet (only in the first few seconds after startup, or if Hacker News was down at startup)

## How it works

To get the best stories you have to call Hacker News once for the list of ids and then once per story
(around 200 calls). If the API did that on every request, lots of requests would mean lots of calls to
Hacker News, which is what the task asks to avoid.

So the API doesn't call Hacker News when a request comes in. Instead:

1. `StoryRefreshService` (a background service) loads the best stories every 60 seconds.
   It fetches the stories in parallel but only 5 at a time.
2. The stories are sorted by score and kept in memory in `StoryCache`.
3. `StoriesController` just takes the first `n` from that list.

This means Hacker News gets the same number of calls every minute no matter how much traffic we get.
I tested it locally with a fake Hacker News: 5000 requests caused only the one background load (1 + 200 calls).

The API request path never calls Hacker News. Only the background refresh does.

Some other details:

- Right after startup, before the first load has finished, the API returns `503` instead of making the
  request wait for Hacker News. The first load usually takes a second or two.
- Only the background service refreshes, so there is never more than one load running at a time.
- Each refresh builds a new list and then swaps it in, so requests never see a half-built list.
- If a refresh fails (Hacker News down), the old list keeps being served.
- If one story fails to load, it is skipped instead of failing the whole refresh.
- Calls to Hacker News use `AddStandardResilienceHandler()`, which retries temporary errors (429, 5xx, timeouts)
  with backoff. It doesn't retry things like 404.

The refresh interval and number of parallel requests can be changed in `appsettings.json`.

## Assumptions

- Data can be up to 60 seconds old. The best stories don't change that fast and it protects Hacker News.
- It runs as a single instance, so an in-memory cache is enough.

## Result
<img width="1156" height="917" alt="image" src="https://github.com/user-attachments/assets/a6e5f696-a627-4514-8376-74f0d725f8eb" />
<img width="1297" height="580" alt="image" src="https://github.com/user-attachments/assets/98b1f970-6e37-4d42-89a2-674b32ad9396" />





