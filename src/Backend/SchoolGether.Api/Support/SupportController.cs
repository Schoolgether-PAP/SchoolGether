using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Support;
using SchoolGether.Contracts.Support;

namespace SchoolGether.Api.Support;

[ApiController, Authorize, Route("api/v1/support")]
public sealed class SupportController(SupportAuthorization authorization, SupportService service,
    SupportQueries queries, ICurrentAccount current) : ControllerBase
{
    [HttpGet("context")]
    public async Task<SupportContextResponse> Context(CancellationToken ct)
    {
        var context = await authorization.ContextAsync(ct);
        return new(context.Enabled, context.CanOpen, context.CanReadGlobal, context.CanManageGlobal);
    }
    [HttpGet("assignees")]
    public async Task<IReadOnlyList<SupportAssigneeResponse>> Assignees(CancellationToken ct) =>
        (await authorization.AssigneesAsync(ct)).Select(x => new SupportAssigneeResponse(x.Id, x.Name)).ToArray();
    [HttpGet("tickets")]
    public async Task<SupportTicketPageResponse> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] string? priority = null,
        [FromQuery] int? institutionId = null, [FromQuery] int? assigneeId = null, CancellationToken ct = default)
    {
        var result = await queries.ListAsync(new(page, pageSize, search, status, priority, institutionId, assigneeId), ct);
        return new(result.Items.Select(Ticket).ToArray(), result.Total, result.Page, result.PageSize);
    }
    [HttpGet("tickets/{id:int}")]
    public async Task<SupportTicketResponse> Get(int id, CancellationToken ct) => Ticket(await queries.GetAsync(id, ct));
    [HttpPost("tickets"), Consumes("multipart/form-data"), SupportMultipart, RequestSizeLimit(16777216)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        using var body = await SupportMultipartRequest<CreateSupportTicketRequest>.ReadAsync(Request, ct);
        var request = body.Request;
        var result = await service.CreateAsync(new(request.ClientId, request.Subject, request.Category, request.Priority, request.Description, body.Files), ct);
        return result.Replay ? Ok(Ticket(result)) : CreatedAtAction(nameof(Get), new { id = result.Id }, Ticket(result));
    }
    [HttpGet("tickets/{id:int}/messages")]
    public async Task<IActionResult> Messages(int id, [FromQuery] int? beforeId = null, [FromQuery] int? afterId = null,
        [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var page = await queries.MessagesAsync(id, beforeId, afterId, take, ct);
        if (page.Administrative)
            return Ok(new SupportAdminMessagePageResponse(page.PublicMessages.Concat(page.InternalMessages).OrderBy(x => x.Id).Select(AdminMessage).ToArray(), page.HasMore));
        return Ok(new SupportMessagePageResponse(page.PublicMessages.Select(Message).ToArray(), page.HasMore));
    }
    [HttpPost("tickets/{id:int}/messages"), Consumes("multipart/form-data"), SupportMultipart, RequestSizeLimit(16777216)]
    public async Task<IActionResult> Send(int id, CancellationToken ct)
    {
        using var body = await SupportMultipartRequest<SendSupportMessageRequest>.ReadAsync(Request, ct);
        var request = body.Request;
        var result = await service.SendAsync(id, new(request.ClientId, request.RowVersion, request.Text, request.Internal, body.Files), ct);
        object response = current.Role == "PlatformAdmin" ? AdminMessage(result) : Message(result);
        return result.Replay ? Ok(response) : CreatedAtAction(nameof(Messages), new { id }, response);
    }
    [HttpPut("tickets/{id:int}")]
    public async Task<SupportTicketResponse> Update(int id, UpdateSupportTicketRequest request, CancellationToken ct) =>
        Ticket(await service.UpdateAsync(id, new(request.RowVersion, request.Status, request.Priority, request.AssigneeId), ct));
    [HttpPost("tickets/{id:int}/close")]
    public async Task<SupportTicketResponse> Close(int id, CloseSupportTicketRequest request, CancellationToken ct) =>
        Ticket(await service.CloseAsync(id, new(request.RowVersion, request.Summary), ct));

    private static SupportTicketResponse Ticket(SupportTicketResult x) => new(x.Id, x.InstitutionId, x.AuthorId, x.AuthorName,
        x.Subject, x.Category, x.Priority, x.Status, x.AssigneeId, x.AssigneeName, x.RowVersion, x.CreatedAt, x.ClosedAt,
        x.ClosedBy, x.CloseSummary, x.CanReply, x.CanManage, x.CanClose);
    private static SupportMessageResponse Message(SupportMessageResult x) => new(x.Id, x.ClientId, x.AuthorId, x.AuthorName,
        x.Text, x.CreatedAt, x.Attachments.Select(Attachment).ToArray());
    private static SupportAdminMessageResponse AdminMessage(SupportMessageResult x) => new(x.Id, x.ClientId, x.AuthorId, x.AuthorName,
        x.Text, x.CreatedAt, x.Attachments.Select(Attachment).ToArray(), x.Internal);
    private static SupportAttachmentResponse Attachment(SupportAttachmentResult x) => new(x.Id, x.FileName, x.ContentType, x.Size);
}
