using CommentService.Data;
using CommentService.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

var serviceName = builder.Environment.ApplicationName;
var seqUrl = builder.Configuration["SeqUrl"]
    ?? "http://localhost:5341";

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(
        ResourceBuilder.CreateDefault().AddService(serviceName));

    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;

    logging.AddOtlpExporter(exporter =>
    {
        exporter.Endpoint =
            new Uri($"{seqUrl}/ingest/otlp/v1/logs");

        exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();

        tracing.AddOtlpExporter(exporter =>
        {
            exporter.Endpoint =
                new Uri($"{seqUrl}/ingest/otlp/v1/traces");

            exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
        });
    });

builder.Services.AddControllers();
builder.Services.AddSingleton<CommentCache>();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

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

var commentCache = app.Services.GetRequiredService<CommentCache>();

var cacheHits = Metrics.CreateCounter(
    "happyheadlines_cache_hits_total",
    "Total cache hits.");

var cacheMisses = Metrics.CreateCounter(
    "happyheadlines_cache_misses_total",
    "Total cache misses.");

Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
{
    var statistics = commentCache.GetStatistics();

    cacheHits.IncTo(statistics.Hits);
    cacheMisses.IncTo(statistics.Misses);
});

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<CommentDbContext>();

    await database.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.MapMetrics();
app.Run();