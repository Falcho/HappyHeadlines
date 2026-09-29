using CommentService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var dataDirectory = Path.Combine(
    builder.Environment.ContentRootPath,
    "comment-data");

Directory.CreateDirectory(dataDirectory);

var databasePath = Path.Combine(dataDirectory, "comments.db");

builder.Services.AddDbContext<CommentDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddHttpClient("ProfanityService", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ProfanityServiceUrl"]
        ?? "http://localhost:5055");

    client.Timeout = Timeout.InfiniteTimeSpan;
})
.AddResilienceHandler("profanity-pipeline", pipeline =>
{
    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 1.0,
        MinimumThroughput = 3,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(15)
    });

    pipeline.AddTimeout(TimeSpan.FromSeconds(3));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<CommentDbContext>();

    await database.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.Run();