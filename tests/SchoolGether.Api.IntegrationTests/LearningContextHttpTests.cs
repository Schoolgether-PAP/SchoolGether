using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class LearningContextHttpTests
{
    [Fact]
    public async Task Contexts_require_active_membership_and_are_tenant_scoped()
    {
        await using var factory = new IdentityFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var ids = await AcademicEnrolmentTests.Setup(factory, client);
        var enrolment = await AcademicCourseTests.Read(await client.PostAsJsonAsync(
            AcademicEnrolmentTests.Route(ids.Course, ids.Class), new { ClientId = Guid.NewGuid(), StudentId = ids.Student }), HttpStatusCode.Created);
        await AcademicCourseTests.Login(client, "student-a@example.test");
        var contexts = await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/contexts?academicYear=2026%2F2027"));
        Assert.Equal(ids.Class, contexts[0].GetProperty("classId").GetInt32());
        await AcademicCourseTests.Login(client, "student-b@example.test");
        Assert.Empty((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/contexts?academicYear=2026%2F2027"))).EnumerateArray());
        await AcademicCourseTests.Login(client, "admin-a@example.test");
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicEnrolmentTests.Route(ids.Course, ids.Class)}/{enrolment.GetProperty("id").GetInt32()}/cancel", new { RowVersion = 1 }));
        await AcademicCourseTests.Login(client, "student-a@example.test");
        Assert.Empty((await AcademicCourseTests.Read(await client.GetAsync("/api/v1/learning/contexts?academicYear=2026%2F2027"))).EnumerateArray());
    }
}
