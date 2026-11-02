using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PublisherService.Models;
using RabbitMQ.Client;

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

builder.Services.AddHttpClient("ProfanityService", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ProfanityServiceUrl"]
        ?? "http://localhost:5055");

    client.Timeout = TimeSpan.FromSeconds(3);
});

var rabbitMqFactory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "student",
    Password = "schoolproject"
};

await using var rabbitMqConnection =
    await rabbitMqFactory.CreateConnectionAsync();

builder.Services.AddSingleton<IConnection>(rabbitMqConnection);
builder.Services.AddSwaggerGen();

await using (var channel =
    await rabbitMqConnection.CreateChannelAsync())
{
    await channel.ExchangeDeclareAsync(
        exchange: "articles.published",
        type: ExchangeType.Fanout,
        durable: true,
        autoDelete: false);

    foreach (var queueName in new[]
    {
        "articles.store",
        "articles.newsletter"
    })
    {
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: "articles.published",
            routingKey: string.Empty);
    }
}

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