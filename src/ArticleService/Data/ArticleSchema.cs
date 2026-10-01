using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public static class ArticleSchema
{
    public static async Task EnsurePublishedAtAsync(
        ArticleDbContext database)
    {
        await database.Database.OpenConnectionAsync();

        try
        {
            var connection = database.Database.GetDbConnection();

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info('Articles');";

            var columnExists = false;

            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1) == "PublishedAt")
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
            {
                await database.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE "Articles"
                    ADD COLUMN "PublishedAt" TEXT NOT NULL
                    DEFAULT '0001-01-01 00:00:00';
                    """);
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }
}