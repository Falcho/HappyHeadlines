using System.Text.Json;
using System.Text.Json.Serialization;
using ArticleService.Models;
using ArticleService.Data;
using RabbitMQ.Client;
using ArticleService.Messaging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ArticleService.Caching;
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
        tracing.AddSource("HappyHeadlines.Messaging");

        tracing.AddOtlpExporter(exporter =>
        {
            exporter.Endpoint =
                new Uri($"{seqUrl}/ingest/otlp/v1/traces");

            exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
        });
    });

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter<ArticleScope>(
                JsonNamingPolicy.SnakeCaseLower,
                allowIntegerValues: false));
    });

builder.Services.AddOpenApi();
builder.Services.AddSingleton<ArticleDbContextFactory>();
builder.Services.AddSingleton<ArticleCache>();
builder.Services.AddHostedService<ArticleCacheWorker>();
builder.Services.AddSwaggerGen();

var rabbitMqFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMq:Host"] ?? "localhost",
    UserName = "student",
    Password = "schoolproject"
};

await using var rabbitMqConnection =
    args.Contains("--initialize-only")
        ? null
        : await rabbitMqFactory.CreateConnectionAsync();

if (rabbitMqConnection is not null)
{
    builder.Services.AddSingleton<IConnection>(rabbitMqConnection);
    builder.Services.AddHostedService<ArticlePublishedConsumer>();
}

var app = builder.Build();

var articleCache = app.Services.GetRequiredService<ArticleCache>();

var cacheHits = Metrics.CreateCounter(
    "happyheadlines_cache_hits_total",
    "Total cache hits.");

var cacheMisses = Metrics.CreateCounter(
    "happyheadlines_cache_misses_total",
    "Total cache misses.");

Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
{
    var statistics = articleCache.GetStatistics();

    cacheHits.IncTo(statistics.Hits);
    cacheMisses.IncTo(statistics.Misses);
});

var databaseFactory = app.Services.GetRequiredService<ArticleDbContextFactory>();

foreach (var scope in Enum.GetValues<ArticleScope>())
{
   await using var database = databaseFactory.Create(scope);
   await database.Database.EnsureCreatedAsync();

   if (args.Contains("--initialize-only"))
   {
       await ArticleSchema.EnsurePublishedAtAsync(database);
       Console.WriteLine($"Database ready: {scope}");
    }
}

if (args.Contains("--initialize-only"))
{
    return;
}

var instanceName =
    Environment.GetEnvironmentVariable("INSTANCE_NAME") ?? "local";

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Article-Service-Instance"] = instanceName;
    await next(context);
});

// Configure the HTTP request pipeline.
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
