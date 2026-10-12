using Microsoft.EntityFrameworkCore;
using SchoolGether.Domain.Institutions;

namespace SchoolGether.Infrastructure.Persistence;

public sealed class SchoolGetherDbContext(DbContextOptions<SchoolGetherDbContext> options)
    : DbContext(options)
{
    public DbSet<Institution> Institutions => Set<Institution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolGetherDbContext).Assembly);
    }
}
