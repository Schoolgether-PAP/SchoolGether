using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Platform;
using SchoolGether.Contracts.Platform;

namespace SchoolGether.Api.Platform;

[ApiController, Authorize]
[Route("api/v1/platform")]
public sealed class PlatformAdminsController(PlatformAdminService admins) : ControllerBase
{
    [HttpGet("context")]
    [ProducesResponseType<PlatformContextResponse>(StatusCodes.Status200OK)]
    public async Task<PlatformContextResponse> Context(CancellationToken cancellationToken)
    {
        var value = await admins.ContextAsync(cancellationToken);
        return new PlatformContextResponse(value.Id, value.IsMaster, value.Permissions, value.RowVersion);
    }

    [HttpGet("admins")]
    [ProducesResponseType<PagedResponse<PlatformAdminResponse>>(StatusCodes.Status200OK)]
    public async Task<PagedResponse<PlatformAdminResponse>> List(CancellationToken cancellationToken, int page = 1, int pageSize = 20)
    {
        var result = await admins.ListAsync(page, pageSize, cancellationToken);
        return new PagedResponse<PlatformAdminResponse>(result.Items.Select(Map).ToArray(), result.Total, result.Page, result.PageSize);
    }

    [HttpPost("admins")]
    [ProducesResponseType<PlatformAdminResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PlatformAdminResponse>> Create(CreatePlatformAdminRequest request, CancellationToken cancellationToken)
    {
        var value = await admins.CreateAsync(new CreatePlatformAdmin(request.Email, request.Password, request.Name, request.Permissions), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, Map(value));
    }

    [HttpPut("admins/{id:int}")]
    [ProducesResponseType<PlatformAdminResponse>(StatusCodes.Status200OK)]
    public async Task<PlatformAdminResponse> Update(int id, UpdatePlatformAdminRequest request, CancellationToken cancellationToken) =>
        Map(await admins.UpdateAsync(id, new UpdatePlatformAdmin(request.Name, request.IsActive, request.Permissions,
            request.RowVersion, request.Reason), cancellationToken));

    [HttpGet("audit")]
    [ProducesResponseType<PagedResponse<AuditResponse>>(StatusCodes.Status200OK)]
    public async Task<PagedResponse<AuditResponse>> Audit(CancellationToken cancellationToken, int page = 1, int pageSize = 20, string? search = null)
    {
        var result = await admins.AuditAsync(page, pageSize, search, cancellationToken);
        return new PagedResponse<AuditResponse>(result.Items.Select(x => new AuditResponse(x.Id,
            x.CreatedAt, x.ActorId, x.ActorName, x.InstitutionId, x.Action, x.Target, x.Result, x.Reason,
            Parse(x.Before), Parse(x.After))).ToArray(), result.Total, result.Page, result.PageSize);
    }

    private static PlatformAdminResponse Map(PlatformAdmin value) => new(value.Id, value.Email, value.Name,
        value.IsMaster, value.IsActive, value.Permissions, value.RowVersion);
    private static JsonElement? Parse(string? json) => json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
