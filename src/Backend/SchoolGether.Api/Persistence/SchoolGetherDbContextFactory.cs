using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.Persistence;

// As ferramentas EF precisam de criar o contexto sem iniciar o servidor HTTP.
public sealed class SchoolGetherDbContextFactory : IDesignTimeDbContextFactory<SchoolGetherDbContext>
{
    public SchoolGetherDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<SchoolGetherDbContextFactory>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SchoolGether");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:SchoolGether antes de executar as ferramentas EF. " +
                "Consulte docs/database.md.");
        }

        var options = new DbContextOptionsBuilder<SchoolGetherDbContext>()
            .UseMySQL(connectionString)
            .Options;

        return new SchoolGetherDbContext(options);
    }
}
