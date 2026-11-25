using System.Net;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Calendar;

namespace SchoolGether.ApiClient.Calendar;

// Shared by clients: a lost response must never turn an edited draft into a second create.
public sealed class CalendarCreateRecovery
{
    public CreateEventRequest? Pending { get; private set; }
    public bool IsUncertain { get; private set; }
    public CreateEventRequest Begin(CreateEventRequest draft) => Pending ??= draft;
    public void Confirm() { Pending = null; IsUncertain = false; }
    public void Fail(Exception error)
    {
        if (Pending is null) return;
        var definite = error is IdentityApiException { StatusCode: { } status }
            && (int)status is >= 400 and < 500 && status != HttpStatusCode.RequestTimeout;
        IsUncertain |= !definite;
        // A later refusal cannot disprove that the earlier, unanswered request committed.
        if (!IsUncertain) Pending = null;
    }
}
