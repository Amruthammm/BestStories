using BestStories.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = Path.Combine(AppContext.BaseDirectory, "BestStories.Api.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["HackerNews:BaseUrl"]!);
    })
    // Retries 429/5xx/timeouts with backoff (not 404s), plus timeouts and a circuit breaker
    .AddStandardResilienceHandler();

// Singleton so every request shares the same in-memory list
builder.Services.AddSingleton<IStoryCache, StoryCache>();
builder.Services.AddHostedService<StoryRefreshService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

// needed for the tests
public partial class Program { }
