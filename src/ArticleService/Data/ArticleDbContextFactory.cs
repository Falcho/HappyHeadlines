using ArticleService.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArticleService.Data;

public class ArticleDbContextFactory
{
    private readonly string _baseConnectionString;

    public ArticleDbContextFactory(IConfiguration configuration)
    {
        _baseConnectionString = configuration.GetConnectionString("ArticleDatabase")
            ?? "Host=localhost;Port=5434;Username=student;Password=schoolproject";
    }

    public ArticleDbContext Create(ArticleScope scope)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), $"Invalid scope value: {scope}");
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = $"articles_{scope.ToString().ToLowerInvariant()}"
        }.ToString();

        var options = new DbContextOptionsBuilder<ArticleDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ArticleDbContext(options);
    }
}