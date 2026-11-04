using SubscriberService.Data;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using SubscriberService.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Release toggle: features.json is re-read when the file changes,
// so the service can be switched on and off without redeploying.
builder.Configuration.AddJsonFile(
    "features.json",
    optional: false,
    reloadOnChange: true);

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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("SubscriberDatabase")
    ?? "Host=localhost;Port=5434;Database=subscribers;Username=student;Password=schoolproject";

builder.Services.AddDbContextPool<SubscriberDbContext>(options =>
    options.UseNpgsql(connectionString));

var rabbitMqFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMq:Host"] ?? "localhost",
    UserName = "student",
    Password = "schoolproject"
};

await using var rabbitMqConnection =
    await rabbitMqFactory.CreateConnectionAsync();

await using (var channel = await rabbitMqConnection.CreateChannelAsync())
{
    await channel.QueueDeclareAsync(
        queue: SubscriberQueuePublisher.QueueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null);
}

builder.Services.AddSingleton<IConnection>(rabbitMqConnection);
builder.Services.AddSingleton<SubscriberQueuePublisher>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<SubscriberDbContext>();

    await database.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Toggle point: while the release toggle is off, the service is hidden
// and answers 404 to every request.
app.Use(async (context, next) =>
{
    if (!app.Configuration.GetValue<bool>("Features:SubscriberService"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next(context);
});

app.UseAuthorization();
app.MapControllers();
app.Run();
