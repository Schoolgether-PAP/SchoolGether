using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class AcademicAtomicityTests
{
    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("archive")]
    [InlineData("restore")]
    public async Task Audit_storage_failure_rolls_back_the_whole_mutation(string operation)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient(); await AcademicCourseTests.Login(client, "admin-a@example.test");
        var key = Guid.NewGuid(); var body = new { ClientId = key, Code = "A", Name = "Inicial" }; var id = 0; long version = 1;
        if (operation != "create")
        {
            var created = await AcademicCourseTests.Read(await client.PostAsJsonAsync(AcademicCourseTests.Route, body), HttpStatusCode.Created); id = created.GetProperty("id").GetInt32();
            if (operation == "restore") { await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{id}/archive", new { RowVersion = 1 })); version = 2; }
        }
        using (var setup = factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_academic_audit BEFORE INSERT ON audit_entry WHEN NEW.action LIKE 'academic.%' AND NEW.result = 'success' BEGIN SELECT RAISE(ABORT, 'academic audit unavailable'); END;");
        }
        using var failed = operation switch
        {
            "create" => await client.PostAsJsonAsync(AcademicCourseTests.Route, body),
            "update" => await client.PutAsJsonAsync($"{AcademicCourseTests.Route}/{id}", new { RowVersion = version, Code = "CHANGED", Name = "Alterado" }),
            _ => await client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{id}/{operation}", new { RowVersion = version })
        };
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using var verify = factory.Services.CreateScope(); var persisted = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var courses = await persisted.Courses.IgnoreQueryFilters().ToListAsync();
        if (operation == "create") Assert.Empty(courses);
        else { var course = Assert.Single(courses); Assert.Equal("Inicial", course.Name); Assert.Equal(version, course.RowVersion); Assert.Equal(operation != "restore", course.IsActive); }
        Assert.Equal(operation == "create" ? 0 : operation == "restore" ? 2 : 1,
            await persisted.AuditEntries.CountAsync(x => x.Action.StartsWith("academic.") && x.Result == "success"));
    }
}
