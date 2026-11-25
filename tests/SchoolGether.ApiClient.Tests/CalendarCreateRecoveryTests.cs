using System.Net;
using SchoolGether.ApiClient.Calendar;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Calendar;

namespace SchoolGether.ApiClient.Tests;

public sealed class CalendarCreateRecoveryTests
{
    private static CreateEventRequest Draft(string title) => new(title, "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid());
    [Fact]
    public void Lost_response_then_refusal_keeps_original_request_until_confirmation()
    {
        var recovery = new CalendarCreateRecovery();
        var original = Draft("Reunião");
        recovery.Begin(original);
        recovery.Fail(new HttpRequestException());
        Assert.True(recovery.IsUncertain);
        Assert.Same(original, recovery.Begin(Draft("Outro evento")));
        recovery.Fail(new IdentityApiException(HttpStatusCode.Conflict, "Conflict", null));
        Assert.Same(original, recovery.Pending);
        recovery.Confirm();
        Assert.Null(recovery.Pending);
        Assert.False(recovery.IsUncertain);
    }
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public void Definite_first_refusal_unlocks_draft(HttpStatusCode status)
    {
        var recovery = new CalendarCreateRecovery();
        recovery.Begin(Draft("Reunião"));
        recovery.Fail(new IdentityApiException(status, "Rejected", null));
        Assert.Null(recovery.Pending);
        Assert.False(recovery.IsUncertain);
    }
}
