using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class LearningMaterialsHttpTests
{
    [Fact]
    public Task Materials_require_current_context_and_students_only_see_published_items() => VerifyAsync(null);

    internal static async Task VerifyAsync(string? connection)
    {
        await using var factory = new IdentityFactory(mySqlConnectionString: connection); await factory.SeedAsync(); using var client = factory.CreateClient();
        var ids = await AcademicEnrolmentTests.Setup(factory, client);
        int teacher, unit;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            teacher = await db.Users.Where(x => x.Email == "teacher-a@example.test").Select(x => x.Id).SingleAsync();
            unit = await db.ClassUnits.IgnoreQueryFilters().Where(x => x.ClassId == ids.Class).Select(x => x.UnitId).SingleAsync();
        }
        var enrolment = await AcademicCourseTests.Read(await client.PostAsJsonAsync(AcademicEnrolmentTests.Route(ids.Course, ids.Class), new { ClientId = Guid.NewGuid(), StudentId = ids.Student }), HttpStatusCode.Created);
        var assignments = AcademicAssignmentTests.Route(ids.Course, ids.Class);
        var assignment = await AcademicCourseTests.Read(await client.PostAsJsonAsync(assignments, new { ClientId = Guid.NewGuid(), TeacherId = teacher, UnitId = unit }), HttpStatusCode.Created);
        var other = await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/identity/users", new { Email = "materials-other@example.test", Password = IdentityFactory.Password, Name = "Outro professor", Role = "Teacher" }), HttpStatusCode.Created);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync(assignments, new { ClientId = Guid.NewGuid(), TeacherId = other.GetProperty("id").GetInt32(), UnitId = unit }), HttpStatusCode.Created);
        await AcademicCourseTests.Login(client, "teacher-a@example.test");
        var body = new { ClientId = Guid.NewGuid(), CourseId = ids.Course, ClassId = ids.Class, UnitId = unit, AcademicYear = "2026/2027", Title = "Ficha", Content = "Texto", Url = "https://example.org/", IsPublished = false };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/learning/materials", new { ClientId = Guid.NewGuid(), body.CourseId, body.ClassId, body.UnitId, body.AcademicYear, body.Title, body.Content, Url = "javascript:alert(1)", body.IsPublished })).StatusCode);
        var created = await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/learning/materials", body), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal(id, (await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/learning/materials", body), HttpStatusCode.Created)).GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/learning/materials", new { body.ClientId, body.CourseId, body.ClassId, body.UnitId, body.AcademicYear, Title = "Outra", body.Content, body.Url, body.IsPublished })).StatusCode);
        await AcademicCourseTests.Login(client, "materials-other@example.test");
        Assert.Empty((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/materials?academicYear=2026%2F2027"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/learning/materials/{id}/archive", new { RowVersion = 1 })).StatusCode);
        await AcademicCourseTests.Login(client, "student-a@example.test");
        Assert.Empty((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/materials?academicYear=2026%2F2027"))).GetProperty("items").EnumerateArray());
        await AcademicCourseTests.Login(client, "teacher-a@example.test");
        var update = new { RowVersion = 1, body.Title, body.Content, body.Url, IsPublished = true };
        await AcademicCourseTests.Read(await client.PutAsJsonAsync($"/api/v1/learning/materials/{id}", update));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/v1/learning/materials/{id}", update)).StatusCode);
        await AcademicCourseTests.Login(client, "student-a@example.test");
        Assert.Single((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/materials?academicYear=2026%2F2027"))).GetProperty("items").EnumerateArray());
        // O login está limitado a 10 pedidos por minuto: reutiliza os tokens já obtidos.
        var student = client.DefaultRequestHeaders.Authorization;
        await AcademicCourseTests.Login(client, "admin-a@example.test");
        var admin = client.DefaultRequestHeaders.Authorization;
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicEnrolmentTests.Route(ids.Course, ids.Class)}/{enrolment.GetProperty("id").GetInt32()}/cancel", new { RowVersion = 1 }));
        client.DefaultRequestHeaders.Authorization = student;
        Assert.Empty((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/materials?academicYear=2026%2F2027"))).GetProperty("items").EnumerateArray());
        await AcademicCourseTests.Login(client, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/learning/materials?academicYear=2026%2F2027")).StatusCode);
        client.DefaultRequestHeaders.Authorization = admin;
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{assignments}/{assignment.GetProperty("id").GetInt32()}/cancel", new { RowVersion = 1 }));
        await AcademicCourseTests.Login(client, "teacher-a@example.test");
        Assert.Empty((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/materials?academicYear=2026%2F2027"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/learning/materials/{id}/archive", new { RowVersion = 2 })).StatusCode);
    }
}
