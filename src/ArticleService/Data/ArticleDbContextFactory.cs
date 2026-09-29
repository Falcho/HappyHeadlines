using ArticleService.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public class ArticleDbContextFactory
{
    private readonly string _dataDirectory;

    public ArticleDbContextFactory(IWebHostEnvironment environment)
    {
        _dataDirectory = Path.Combine(environment.ContentRootPath, "article-data");
        Directory.CreateDirectory(_dataDirectory);
    }

    public ArticleDbContext Create(ArticleScope scope)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), $"Invalid scope value: {scope}");
        }

        var databasePath = Path.Combine(_dataDirectory, $"{scope}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();

        var options = new DbContextOptionsBuilder<ArticleDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new ArticleDbContext(options);
        
    }
}