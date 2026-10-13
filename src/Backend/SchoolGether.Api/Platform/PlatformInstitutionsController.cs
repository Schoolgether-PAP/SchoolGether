using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Institutions;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Platform;

namespace SchoolGether.Api.Platform;

[ApiController, Authorize]
[Route("api/v1/platform/institutions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformInstitutionsController(PlatformInstitutionService institutions) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<PlatformInstitutionResponse>>(StatusCodes.Status200OK)]
    public async Task<PagedResponse<PlatformInstitutionResponse>> List(CancellationToken cancellationToken,
        int page = 1, int pageSize = 20, string? search = null, bool? isSuspended = null)
    {
        var result = await institutions.ListAsync(page, pageSize, search, isSuspended, cancellationToken);
        return new PagedResponse<PlatformInstitutionResponse>(result.Items.Select(Map).ToArray(), result.Total, result.Page, result.PageSize);
    }
    [HttpGet("{id:int}")]
    [ProducesResponseType<PlatformInstitutionDetailResponse>(StatusCodes.Status200OK)]
    public async Task<PlatformInstitutionDetailResponse> Detail(int id, CancellationToken cancellationToken)
    {
        var result = await institutions.DetailAsync(id, cancellationToken);
        return new PlatformInstitutionDetailResponse(Map(result.Institution), result.Administrators.Select(MapAccount).ToArray());
    }
    [HttpPost]
    [ProducesResponseType<PlatformInstitutionResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PlatformInstitutionResponse>> Create(CreatePlatformInstitutionRequest request, CancellationToken cancellationToken)
    {
        var result = await institutions.CreateAsync(new CreatePlatformInstitution(request.Name, request.Reason), cancellationToken);
        return CreatedAtAction(nameof(Detail), new { id = result.Id }, Map(result));
    }
    [HttpPut("{id:int}")]
    [ProducesResponseType<PlatformInstitutionResponse>(StatusCodes.Status200OK)]
    public async Task<PlatformInstitutionResponse> Update(int id, UpdatePlatformInstitutionRequest request, CancellationToken cancellationToken) =>
        Map(await institutions.UpdateAsync(id, new UpdatePlatformInstitution(request.Name, request.RowVersion, request.Reason), cancellationToken));
    [HttpPut("{id:int}/suspension")]
    [ProducesResponseType<PlatformInstitutionResponse>(StatusCodes.Status200OK)]
    public async Task<PlatformInstitutionResponse> SetSuspension(int id, SetInstitutionSuspensionRequest request, CancellationToken cancellationToken) =>
        Map(await institutions.SetSuspensionAsync(id, new SetInstitutionSuspension(request.IsSuspended, request.RowVersion, request.Reason), cancellationToken));
    [HttpPost("{id:int}/administrators")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AccountResponse>> CreateAdministrator(int id, CreateInstitutionAdminRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, MapAccount(await institutions.CreateAdministratorAsync(id,
            new CreateInstitutionAdministrator(request.Email, request.Password, request.Name, request.Reason), cancellationToken)));
    private static PlatformInstitutionResponse Map(PlatformInstitution value) => new(value.Id, value.Name, value.IsSuspended,
        value.SuspensionReason, value.CreatedAt, value.AccountsCount, value.RowVersion);
    private static AccountResponse MapAccount(SchoolGether.Application.Identity.Account value) =>
        new(value.Id, value.Email, value.Name, value.Role.ToString(), value.InstitutionId);
}
