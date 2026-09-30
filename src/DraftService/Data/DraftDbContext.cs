using DraftService.Models;
using Microsoft.EntityFrameworkCore;

namespace DraftService.Data;

public class DraftDbContext : DbContext
{
    public DraftDbContext(
        DbContextOptions<DraftDbContext> options)
        : base(options)
    {
    }

    public DbSet<Draft> Drafts => Set<Draft>();
}