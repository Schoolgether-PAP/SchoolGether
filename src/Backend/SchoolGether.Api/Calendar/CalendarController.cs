using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Calendar;
using SchoolGether.Contracts.Calendar;
namespace SchoolGether.Api.Calendar;

[ApiController, Authorize, Route("api/v1/calendar/events")]
public sealed class CalendarController(CalendarService service) : ControllerBase
{
    [HttpGet]
    public async Task<CalendarPageResponse> List([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, [FromQuery] string status = "Published", CancellationToken ct = default)
    { var p = await service.ListAsync(new(from, to, page, pageSize, status), ct); return new(p.Items.Select(Map).ToArray(), p.Total, p.Page, p.PageSize); }
    [HttpPost] public async Task<IActionResult> Create(CreateEventRequest r, CancellationToken ct) { var value = await service.CreateAsync(new(r.Title, r.Details, r.Location, r.Start, r.End, r.ClientId), ct); return StatusCode(201, Map(value)); }
    [HttpPut("{id:int}")] public async Task<EventResponse> Update(int id, UpdateEventRequest r, CancellationToken ct) => Map(await service.ChangeAsync(id, r.RowVersion, "update", new(r.Title, r.Details, r.Location, r.Start, r.End), ct));
    [HttpPost("{id:int}/approve")] public async Task<EventResponse> Approve(int id, EventStateRequest r, CancellationToken ct) => Map(await service.ChangeAsync(id, r.RowVersion, "approve", null, ct));
    [HttpPost("{id:int}/reject")] public async Task<EventResponse> Reject(int id, EventStateRequest r, CancellationToken ct) => Map(await service.ChangeAsync(id, r.RowVersion, "reject", null, ct));
    [HttpPost("{id:int}/cancel")] public async Task<EventResponse> Cancel(int id, EventStateRequest r, CancellationToken ct) => Map(await service.ChangeAsync(id, r.RowVersion, "cancel", null, ct));
    static EventResponse Map(EventResult e) => new(e.Id, e.Title, e.Details, e.Location, e.Start, e.End, e.Status, e.RowVersion, e.CreatedBy);
}
