using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlCalendarTests
{
    [MySqlFact]
    public async Task Calendar_migration_replays_and_concurrent_updates_preserve_one_tenant_event()
    {
        await using var factory = new IdentityFactory(mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"));
        await factory.SeedAsync();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        await AcademicCourseTests.Login(first, "teacher-a@example.test");
        await AcademicCourseTests.Login(second, "teacher-a@example.test");
        var route = "/api/v1/calendar/events";
        var start = DateTimeOffset.Parse("2027-05-12T09:00:00Z");
        var body = new { ClientId = Guid.NewGuid(), Title = "Visita", Start = start, End = start.AddHours(1) };
        var attempts = await Task.WhenAll(first.PostAsJsonAsync(route, body), second.PostAsJsonAsync(route, body));
        try
        {
            Assert.All(attempts, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
            Assert.Contains(attempts, r => r.StatusCode == HttpStatusCode.Created);
        }
        finally { foreach (var response in attempts) response.Dispose(); }
        var replay = await AcademicCourseTests.Read(await first.PostAsJsonAsync(route, body), HttpStatusCode.Created);
        var id = replay.GetProperty("id").GetInt32();
        await AcademicCourseTests.Login(first, "admin-a@example.test");
        await AcademicCourseTests.Login(second, "admin-a@example.test");
        var changes = await Task.WhenAll(
            first.PostAsJsonAsync($"{route}/{id}/approve", new { RowVersion = 1 }),
            second.PostAsJsonAsync($"{route}/{id}/approve", new { RowVersion = 1 }));
        try { Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, changes.Select(r => r.StatusCode).Order().ToArray()); }
        finally { foreach (var response in changes) response.Dispose(); }
        await AcademicCourseTests.Login(second, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await second.PostAsJsonAsync($"{route}/{id}/cancel", new { RowVersion = 2 })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await db.CalendarEvents.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await db.AuditEntries.CountAsync(e => e.Action.StartsWith("calendar.") && e.Result == "success"));
    }
}
