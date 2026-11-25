using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
namespace SchoolGether.Api.IntegrationTests;

public sealed class CalendarTests
{
    const string Route = "/api/v1/calendar/events";
    [Fact]
    public async Task Retrying_the_same_create_does_not_duplicate_or_overwrite_an_event()
    {
        await using var f = new IdentityFactory();
        await f.SeedAsync();
        using var c = f.CreateClient();
        await AcademicCourseTests.Login(c, "teacher-a@example.test");
        var key = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2027-06-01T09:00:00Z");
        var body = new { ClientId = key, Title = "Visita", Start = start, End = start.AddHours(1) };
        var first = await AcademicCourseTests.Read(await c.PostAsJsonAsync(Route, body), HttpStatusCode.Created);
        var again = await AcademicCourseTests.Read(await c.PostAsJsonAsync(Route, body), HttpStatusCode.Created);
        Assert.Equal(first.GetProperty("id").GetInt32(), again.GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Route, new { ClientId = key, Title = "Outra visita", Start = start, End = start.AddHours(1) })).StatusCode);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGether.Infrastructure.Persistence.SchoolGetherDbContext>();
        Assert.Equal(1, await db.CalendarEvents.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.Action == "calendar.create"));
    }
    [Fact]
    public async Task Proposal_approval_tenant_and_concurrency_are_enforced()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var c = factory.CreateClient();
        await AcademicCourseTests.Login(c, "teacher-a@example.test");
        var body = new { Title = "Visita", Details = "Museu", Location = "Lisboa", Start = DateTimeOffset.Parse("2027-03-28T09:00:00Z"), End = DateTimeOffset.Parse("2027-03-28T11:00:00Z") };
        var created = await AcademicCourseTests.Read(await c.PostAsJsonAsync(Route, body), HttpStatusCode.Created); var id = created.GetProperty("id").GetInt32();
        Assert.Equal("Pending", created.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync($"{Route}/{id}/approve", new { RowVersion = 1 })).StatusCode);
        await AcademicCourseTests.Login(c, "student-a@example.test");
        var path = Route + "?from=2027-03-01T00%3A00%3A00Z&to=2027-04-01T00%3A00%3A00Z";
        Assert.Equal(0, (await AcademicCourseTests.Read(await c.GetAsync(path))).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync(Route, body)).StatusCode);
        await AcademicCourseTests.Login(c, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync($"{Route}/{id}/approve", new { RowVersion = 1 })).StatusCode);
        await AcademicCourseTests.Login(c, "admin-a@example.test");
        await AcademicCourseTests.Read(await c.PostAsJsonAsync($"{Route}/{id}/approve", new { RowVersion = 1 }));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Route}/{id}/cancel", new { RowVersion = 1 })).StatusCode);
        await AcademicCourseTests.Login(c, "student-a@example.test");
        Assert.Equal(1, (await AcademicCourseTests.Read(await c.GetAsync(path))).GetProperty("total").GetInt32());
        await AcademicCourseTests.Login(c, "platform@example.test"); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Invalid_dates_range_and_request_status_are_rejected()
    {
        await using var f = new IdentityFactory(); await f.SeedAsync(); using var c = f.CreateClient(); await AcademicCourseTests.Login(c, "teacher-a@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Route, new { Title = "A", Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(-1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Route, new { Title = "A", Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1), Status = "Published" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync(Route + "?from=2026-01-01&to=2028-01-01")).StatusCode);
    }
    [Fact]
    public async Task Teacher_can_edit_cancel_own_pending_but_not_published_and_audits_are_atomic()
    {
        await using var f = new IdentityFactory(); await f.SeedAsync(); using var c = f.CreateClient(); await AcademicCourseTests.Login(c, "teacher-a@example.test");
        var start = DateTimeOffset.Parse("2027-06-01T10:00:00+01:00");
        var created = await AcademicCourseTests.Read(await c.PostAsJsonAsync(Route, new { Title = "Proposta", Start = start, End = start.AddHours(1) }), HttpStatusCode.Created); var id = created.GetProperty("id").GetInt32();
        var edited = await AcademicCourseTests.Read(await c.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = 1, Title = "Editada", Start = start, End = start.AddHours(2) })); Assert.Equal("2027-06-01T09:00:00+00:00", edited.GetProperty("start").GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:sszzz"));
        await AcademicCourseTests.Read(await c.PostAsJsonAsync($"{Route}/{id}/cancel", new { RowVersion = 2 }));
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = 3, Title = "Impossível", Start = start, End = start.AddHours(2) })).StatusCode);
        await AcademicCourseTests.Login(c, "admin-a@example.test"); var published = await AcademicCourseTests.Read(await c.PostAsJsonAsync(Route, new { Title = "Publicada", Start = start, End = start.AddHours(1) }), HttpStatusCode.Created); Assert.Equal("Published", published.GetProperty("status").GetString()); var publicId = published.GetProperty("id").GetInt32();
        await AcademicCourseTests.Login(c, "teacher-a@example.test"); Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync($"{Route}/{publicId}/cancel", new { RowVersion = 1 })).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGether.Infrastructure.Persistence.SchoolGetherDbContext>(); Assert.Equal(4, await db.AuditEntries.CountAsync(x => x.Action.StartsWith("calendar.") && x.Result == "success"));
    }
    [Fact]
    public async Task Unguarded_calendar_writes_are_denied()
    {
        await using var f = new IdentityFactory(); await f.SeedAsync(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGether.Infrastructure.Persistence.SchoolGetherDbContext>();
        var start = DateTimeOffset.UtcNow; db.CalendarEvents.Add(SchoolGether.Domain.Calendar.SchoolEvent.Create(f.InstitutionA, 1, true, "Vedado", null, null, start, start.AddHours(1), start.UtcDateTime));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); Assert.Equal("CalendarWriteDenied", exception.Message);
    }
}
