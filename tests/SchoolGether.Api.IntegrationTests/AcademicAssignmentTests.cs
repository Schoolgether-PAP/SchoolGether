using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Api.IntegrationTests;

public sealed class AcademicAssignmentTests
{
    internal static string Route(int course, int group) => $"{AcademicClassTests.Route(course)}/{group}/assignments";
    [Fact]
    public async Task Assignment_requires_current_tenant_teacher_selected_unit_and_active_membership()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient();
        var ids = await AcademicEnrolmentTests.Setup(factory, client);
        int teacher, outsider, unit;
        using (var services = factory.Services.CreateScope())
        {
            var db = services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            teacher = await db.Users.Where(x => x.Email == "teacher-a@example.test").Select(x => x.Id).SingleAsync();
            outsider = await db.Users.Where(x => x.Email == "student-b@example.test").Select(x => x.Id).SingleAsync();
            unit = await db.ClassUnits.IgnoreQueryFilters().Where(x => x.ClassId == ids.Class).Select(x => x.UnitId).SingleAsync();
        }
        var route = Route(ids.Course, ids.Class);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), TeacherId = outsider, UnitId = unit })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), TeacherId = ids.Student, UnitId = unit })).StatusCode);
        var otherUnit = await AcademicClassTests.Unit(client, ids.Course, "OUT");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), TeacherId = teacher, UnitId = otherUnit })).StatusCode);
        var key = Guid.NewGuid(); var body = new { ClientId = key, TeacherId = teacher, UnitId = unit };
        var created = await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, body), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal(unit, created.GetProperty("unit").GetProperty("id").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("unit").GetProperty("name").GetString()));
        await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, body));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), TeacherId = teacher, UnitId = unit })).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/cancel", new { RowVersion = 1 }));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{route}/{id}/restore", new { RowVersion = 1 })).StatusCode);
        using (var services = factory.Services.CreateScope())
            await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE academic_class_unit SET is_active={false} WHERE class_id={ids.Class} AND unit_id={unit}");
        var historical = await AcademicCourseTests.Read(await client.GetAsync($"{route}/{id}"));
        Assert.Equal(unit, historical.GetProperty("unit").GetProperty("id").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(historical.GetProperty("unit").GetProperty("code").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{route}/{id}/restore", new { RowVersion = 2 })).StatusCode);
        using (var services = factory.Services.CreateScope())
            await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE academic_class_unit SET is_active={true} WHERE class_id={ids.Class} AND unit_id={unit}");
        using (var services = factory.Services.CreateScope())
            await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE user_account SET is_active={false} WHERE id={teacher}");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{route}/{id}/restore", new { RowVersion = 2 })).StatusCode);
        await AcademicCourseTests.Login(client, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
        foreach (var email in new[] { "teacher-a@example.test", "student-a@example.test" })
        {
            if (email.StartsWith("teacher"))
            {
                using var services = factory.Services.CreateScope();
                await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE user_account SET is_active={true} WHERE id={teacher}");
            }
            await AcademicCourseTests.Login(client, email);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(route, body)).StatusCode);
        }
    }
}
