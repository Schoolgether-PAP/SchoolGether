using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Institutions;
using SchoolGether.Domain.Identity;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class IdentityFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test-Only-Password42!";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly IInstitutionDirectory? institutionDirectory;
    private readonly string? mySqlConnectionString;
    public string PrivateKey { get; }
    public int InstitutionA { get; private set; }
    public int InstitutionB { get; private set; }

    public IdentityFactory(IInstitutionDirectory? institutionDirectory = null, string? mySqlConnectionString = null)
    {
        this.institutionDirectory = institutionDirectory;
        this.mySqlConnectionString = mySqlConnectionString;
        if (mySqlConnectionString is null) connection.Open();
        else
        {
            var settings = new MySqlConnectionStringBuilder(mySqlConnectionString);
            if (settings.Database != "schoolgether_test_identity" ||
                settings.Server is not ("localhost" or "127.0.0.1"))
                throw new ArgumentException("O teste MySQL exige a base schoolgether_test_identity num servidor local.");
        }
        using var rsa = RSA.Create(2048);
        PrivateKey = rsa.ExportRSAPrivateKeyPem();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Jwt:PrivateKeyPem"] = PrivateKey }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SchoolGetherDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SchoolGetherDbContext>>();
            services.AddDbContext<SchoolGetherDbContext>(options =>
            {
                if (mySqlConnectionString is null) options.UseSqlite(connection);
                else options.UseMySQL(mySqlConnectionString);
            });
            if (institutionDirectory is not null)
            {
                services.RemoveAll<IInstitutionDirectory>();
                services.AddSingleton<IInstitutionDirectory>(institutionDirectory);
            }
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        if (mySqlConnectionString is null) await db.Database.EnsureCreatedAsync();
        else await db.Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var platform = new ApplicationUser { UserName = "platform@example.test", Email = "platform@example.test",
            Name = "Plataforma", IsPlatformAdmin = true, LockoutEnabled = true };
        Assert.True((await users.CreateAsync(platform, Password)).Succeeded);
        var a = new Institution("Instituição A", platform.Id);
        var b = new Institution("Instituição B", platform.Id);
        db.Institutions.AddRange(a, b);
        await db.SaveChangesAsync();
        InstitutionA = a.Id;
        InstitutionB = b.Id;
        // Contexto explícito do administrador para preparar os dados, sem contornar a guarda de escrita.
        await using var seedDb = new SchoolGetherDbContext(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<SchoolGetherDbContext>>(), new SeedAccount(platform.Id));
        foreach (var (email, institution, role) in new[] {
            ("admin-a@example.test", a.Id, AccountRole.InstitutionAdmin),
            ("admin-b@example.test", b.Id, AccountRole.InstitutionAdmin),
            ("student-a@example.test", a.Id, AccountRole.Student),
            ("teacher-a@example.test", a.Id, AccountRole.Teacher),
            ("student-b@example.test", b.Id, AccountRole.Student) })
        {
            var user = new ApplicationUser { UserName = email, Email = email, Name = email, LockoutEnabled = true };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            seedDb.Members.Add(new InstitutionMember(user.Id, institution, email, role, platform.Id));
        }
        await seedDb.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
    }

    private sealed class SeedAccount(int userId) : ICurrentAccount
    {
        public int? UserId => userId;
        public int? InstitutionId => null;
        public string? Role => nameof(AccountRole.PlatformAdmin);
    }
}
