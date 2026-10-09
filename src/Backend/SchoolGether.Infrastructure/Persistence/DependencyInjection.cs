using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Diagnostics;

namespace SchoolGether.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDatabaseReadiness, DatabaseReadiness>();
        services.AddDbContext<SchoolGetherDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("SchoolGether");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:SchoolGether com user-secrets em desenvolvimento " +
                    "ou com variáveis de ambiente no servidor. Consulte docs/database.md.");
            }

            options.UseMySQL(connectionString);
        });

        return services;
    }
}
