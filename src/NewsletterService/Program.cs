using System.Text.Json;
using System.Text.Json.Serialization;
using NewsletterService.Messaging;
using NewsletterService.Models;
using RabbitMQ.Client;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using NewsletterService.Services;

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
builder.Services.AddHttpClient("ArticleService", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ArticleServiceUrl"]
        ?? "http://localhost:8080");

    client.Timeout = TimeSpan.FromSeconds(10);
});

// SubscriberService lives in its own swimlane. Timeout and circuit breaker
// stop a slow or failing SubscriberService from dragging NewsletterService down.
builder.Services.AddHttpClient("SubscriberService", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["SubscriberServiceUrl"]
        ?? "http://localhost:5060");

    client.Timeout = Timeout.InfiniteTimeSpan;
})
.AddResilienceHandler("subscriber-pipeline", pipeline =>
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

builder.Services.AddSingleton<SubscriberClient>();
builder.Services.AddSingleton<DailyNewsletterSender>();
builder.Services.AddHostedService<DailyNewsletterWorker>();
builder.Services.AddSwaggerGen();

var rabbitMqFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMq:Host"] ?? "localhost",
    UserName = "student",
    Password = "schoolproject"
};

await using var rabbitMqConnection =
    await rabbitMqFactory.CreateConnectionAsync();

builder.Services.AddSingleton<IConnection>(rabbitMqConnection);
builder.Services.AddHostedService<ArticlePublishedConsumer>();
builder.Services.AddHostedService<WelcomeMailConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

await app.RunAsync();