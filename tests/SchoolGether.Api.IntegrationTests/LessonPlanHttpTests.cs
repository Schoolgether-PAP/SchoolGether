using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class LessonPlanHttpTests
{
    [Fact]
    public Task Teacher_plan_requires_active_assignment_and_is_private() => VerifyAsync(null);

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
        var assignments = AcademicAssignmentTests.Route(ids.Course, ids.Class);
        var assignment = await AcademicCourseTests.Read(await client.PostAsJsonAsync(assignments, new { ClientId = Guid.NewGuid(), TeacherId = teacher, UnitId = unit }), HttpStatusCode.Created);
        var secondTeacher = await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/identity/users", new { Email = "teacher-second@example.test", Password = IdentityFactory.Password, Name = "Second teacher", Role = "Teacher" }), HttpStatusCode.Created);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync(assignments, new { ClientId = Guid.NewGuid(), TeacherId = secondTeacher.GetProperty("id").GetInt32(), UnitId = unit }), HttpStatusCode.Created);
        await AcademicCourseTests.Login(client, "teacher-a@example.test");
        var body = new { ClientId = Guid.NewGuid(), CourseId = ids.Course, ClassId = ids.Class, UnitId = unit, AcademicYear = "2026/2027", Title = "Aula", Date = "2026-11-25", Objectives = "Objetivos", Content = "Conteúdo", Resources = "Recursos" };
        var created = await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/learning/lesson-plans", body), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal(id, (await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/learning/lesson-plans", body), HttpStatusCode.Created)).GetProperty("id").GetInt32());
        var update = new { RowVersion = 1, Title = "Aula revista", Date = "2026-11-26", Objectives = "O", Content = "C", Resources = "R" };
        await AcademicCourseTests.Read(await client.PutAsJsonAsync($"/api/v1/learning/lesson-plans/{id}", update));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/v1/learning/lesson-plans/{id}", update)).StatusCode);
        if (connection is not null)
        {
            using var concurrent = factory.CreateClient();
            await AcademicCourseTests.Login(concurrent, "teacher-a@example.test");
            var revision = new { RowVersion = 2, Title = "Revisão concorrente", update.Date, update.Objectives, update.Content, update.Resources };
            var attempts = await Task.WhenAll(client.PutAsJsonAsync($"/api/v1/learning/lesson-plans/{id}", revision), concurrent.PutAsJsonAsync($"/api/v1/learning/lesson-plans/{id}", revision));
            try { Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, attempts.Select(x => x.StatusCode).Order().ToArray()); }
            finally { foreach (var attempt in attempts) attempt.Dispose(); }
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.Action == "learning.plan.update" && x.Result == "success"));
        }
        Assert.Equal(id, (await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/learning/lesson-plans", body), HttpStatusCode.Created)).GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/learning/lesson-plans", new { body.ClientId, body.CourseId, body.ClassId, body.UnitId, body.AcademicYear, Title = "Diferente", body.Date, body.Objectives, body.Content, body.Resources })).StatusCode);
        var second = await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/learning/lesson-plans", new { ClientId = Guid.NewGuid(), body.CourseId, body.ClassId, body.UnitId, body.AcademicYear, body.Title, body.Date, body.Objectives, body.Content, body.Resources }), HttpStatusCode.Created);
        var secondId = second.GetProperty("id").GetInt32();
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"/api/v1/learning/lesson-plans/{secondId}/archive", new { RowVersion = 1 }));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/learning/lesson-plans/{secondId}/archive", new { RowVersion = 1 })).StatusCode);
        await AcademicCourseTests.Login(client, "teacher-second@example.test");
        Assert.Empty((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/lesson-plans?academicYear=2026%2F2027"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/learning/lesson-plans/{id}/archive", new { RowVersion = 2 })).StatusCode);
        await AcademicCourseTests.Login(client, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/learning/lesson-plans?academicYear=2026%2F2027")).StatusCode);
        await AcademicCourseTests.Login(client, "student-a@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/learning/lesson-plans?academicYear=2026%2F2027")).StatusCode);
        await AcademicCourseTests.Login(client, "admin-a@example.test");
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{assignments}/{assignment.GetProperty("id").GetInt32()}/cancel", new { RowVersion = 1 }));
        await AcademicCourseTests.Login(client, "teacher-a@example.test");
        Assert.Empty((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/lesson-plans?academicYear=2026%2F2027"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/learning/lesson-plans/{id}/archive", new { RowVersion = 1 })).StatusCode);
    }
}
