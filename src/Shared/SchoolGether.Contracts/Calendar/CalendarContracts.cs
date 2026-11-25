using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
namespace SchoolGether.Contracts.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateEventRequest([Required, MaxLength(200)] string Title, [MaxLength(4000)] string? Details, [MaxLength(200)] string? Location, DateTimeOffset Start, DateTimeOffset End, Guid? ClientId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEventRequest(long RowVersion, [Required, MaxLength(200)] string Title, [MaxLength(4000)] string? Details, [MaxLength(200)] string? Location, DateTimeOffset Start, DateTimeOffset End);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EventStateRequest(long RowVersion);
public sealed record EventResponse(int Id, [Required, MaxLength(200)] string Title, [MaxLength(4000)] string? Details, [MaxLength(200)] string? Location, DateTimeOffset Start, DateTimeOffset End, string Status, long RowVersion, int CreatedBy);
public sealed record CalendarPageResponse(IReadOnlyList<EventResponse> Items, int Total, int Page, int PageSize);
