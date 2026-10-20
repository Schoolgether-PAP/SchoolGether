using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Academic;
using SchoolGether.Contracts.Academic;

namespace SchoolGether.Api.Academic;

[ApiController, Authorize, Route("api/v1/academic/courses/{courseId:int}/units")]
public sealed class AcademicCourseUnitController(AcademicCourseUnitService service) : ControllerBase
{
    [HttpGet]
    public async Task<CourseUnitPageResponse> List(int courseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string status = "Active", CancellationToken ct = default)
    { var result = await service.ListAsync(courseId, new(page, pageSize, search, status), ct); return new(result.Items.Select(Map).ToArray(), result.Total, result.Page, result.PageSize); }
    [HttpGet("{id:int}")]
    public async Task<CourseUnitResponse> Get(int courseId, int id, CancellationToken ct) => Map(await service.GetAsync(courseId, id, ct));
    [HttpPost]
    public async Task<IActionResult> Create(int courseId, CreateCourseUnitRequest request, CancellationToken ct)
    {
        var value = Map(await service.CreateAsync(courseId, new(request.ClientId, request.Code, request.Name, request.Syllabus), ct));
        return value.Replay ? Ok(value) : CreatedAtAction(nameof(Get), new { courseId, id = value.Id }, value);
    }
    [HttpPut("{id:int}")]
    public async Task<CourseUnitResponse> Update(int courseId, int id, UpdateCourseUnitRequest request, CancellationToken ct) =>
        Map(await service.UpdateAsync(courseId, id, new(request.RowVersion, request.Code, request.Name, request.Syllabus), ct));
    [HttpPost("{id:int}/archive")]
    public async Task<CourseUnitResponse> Archive(int courseId, int id, CourseUnitStateRequest request, CancellationToken ct) => Map(await service.ArchiveAsync(courseId, id, request.RowVersion, ct));
    [HttpPost("{id:int}/restore")]
    public async Task<CourseUnitResponse> Restore(int courseId, int id, CourseUnitStateRequest request, CancellationToken ct) => Map(await service.RestoreAsync(courseId, id, request.RowVersion, ct));
    private static CourseUnitResponse Map(CourseUnitResult x) => new(x.Id, x.InstitutionId, x.CourseId, x.ClientId, x.Code, x.Name, x.Syllabus,
        x.IsActive, x.RowVersion, x.CreatedAt, x.UpdatedAt, x.Replay);
}
