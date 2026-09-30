using DraftService.Data;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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

        tracing.AddOtlpExporter(exporter =>
        {
            exporter.Endpoint =
                new Uri($"{seqUrl}/ingest/otlp/v1/traces");

            exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
        });
    });

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var dataDirectory = Path.Combine(
    builder.Environment.ContentRootPath,
    "draft-data");

Directory.CreateDirectory(dataDirectory);

var databasePath = Path.Combine(dataDirectory, "drafts.db");

builder.Services.AddDbContext<DraftDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<DraftDbContext>();

    await database.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.Run();