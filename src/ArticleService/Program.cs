using System.Text.Json;
using System.Text.Json.Serialization;
using ArticleService.Models;
using ArticleService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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

var app = builder.Build();

var databaseFactory = app.Services.GetRequiredService<ArticleDbContextFactory>();

foreach (var scope in Enum.GetValues<ArticleScope>())
{
   await using var database = databaseFactory.Create(scope);
   await database.Database.EnsureCreatedAsync();
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
}

app.UseAuthorization();

app.MapControllers();

app.Run();
