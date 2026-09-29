using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;
using ProfanityService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var dataDirectory = Path.Combine(
    builder.Environment.ContentRootPath,
    "profanity-data");

Directory.CreateDirectory(dataDirectory);

var databasePath = Path.Combine(dataDirectory, "profanity.db");

builder.Services.AddDbContext<ProfanityDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<ProfanityDbContext>();

    await database.Database.EnsureCreatedAsync();

    if (!await database.ProhibitedWords.AnyAsync())
    {
        database.ProhibitedWords.AddRange(
            new ProhibitedWord { Word = "idiot" },
            new ProhibitedWord { Word = "stupid" });

        await database.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.Run();