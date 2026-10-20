using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Academic;
using SchoolGether.Contracts.Academic;

namespace SchoolGether.Api.Academic;

[ApiController, Authorize, Route("api/v1/academic/courses")]
public sealed class AcademicCourseController(AcademicCourseService service) : ControllerBase
{
    [HttpGet]
    public async Task<CoursePageResponse> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string status = "Active", CancellationToken ct = default)
    { var result = await service.ListAsync(new(page, pageSize, search, status), ct); return new(result.Items.Select(Map).ToArray(), result.Total, result.Page, result.PageSize); }
    [HttpGet("{id:int}")]
    public async Task<CourseResponse> Get(int id, CancellationToken ct) => Map(await service.GetAsync(id, ct));
    [HttpPost]
    public async Task<IActionResult> Create(CreateCourseRequest request, CancellationToken ct)
    {
        var value = Map(await service.CreateAsync(new(request.ClientId, request.Code, request.Name, request.Description), ct));
        return value.Replay ? Ok(value) : CreatedAtAction(nameof(Get), new { id = value.Id }, value);
    }
    [HttpPut("{id:int}")]
    public async Task<CourseResponse> Update(int id, UpdateCourseRequest request, CancellationToken ct) =>
        Map(await service.UpdateAsync(id, new(request.RowVersion, request.Code, request.Name, request.Description), ct));
    [HttpPost("{id:int}/archive")]
    public async Task<CourseResponse> Archive(int id, CourseStateRequest request, CancellationToken ct) => Map(await service.ArchiveAsync(id, request.RowVersion, ct));
    [HttpPost("{id:int}/restore")]
    public async Task<CourseResponse> Restore(int id, CourseStateRequest request, CancellationToken ct) => Map(await service.RestoreAsync(id, request.RowVersion, ct));
    private static CourseResponse Map(CourseResult x) => new(x.Id, x.InstitutionId, x.ClientId, x.Code, x.Name, x.Description,
        x.IsActive, x.RowVersion, x.CreatedAt, x.UpdatedAt, x.Replay);
}
