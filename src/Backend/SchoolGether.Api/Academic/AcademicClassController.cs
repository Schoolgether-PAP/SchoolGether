using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Academic;
using SchoolGether.Contracts.Academic;
namespace SchoolGether.Api.Academic;

[ApiController, Authorize, Route("api/v1/academic/courses/{courseId:int}/classes")]
public sealed class AcademicClassController(AcademicClassService service) : ControllerBase
{
    [HttpGet]
    public async Task<AcademicClassPageResponse> List(int courseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] string status = "Active", [FromQuery] int? studyYear = null, [FromQuery] string? academicYear = null, CancellationToken ct = default)
    {
        var result = await service.ListAsync(courseId, new(page, pageSize, search, status, studyYear, academicYear), ct);
        return new(result.Items.Select(x => new AcademicClassListItemResponse(x.Id, x.InstitutionId, x.CourseId, x.ClientId, x.Code, x.Name, x.StudyYear, x.AcademicYear, x.IsActive, x.RowVersion, x.CreatedAt, x.UpdatedAt, x.UnitCount)).ToArray(), result.Total, result.Page, result.PageSize);
    }
    [HttpGet("{id:int}")]
    public async Task<AcademicClassResponse> Get(int courseId, int id, CancellationToken ct) => Map(await service.GetAsync(courseId, id, ct));
    [HttpPost]
    public async Task<IActionResult> Create(int courseId, CreateAcademicClassRequest request, CancellationToken ct)
    {
        var result = Map(await service.CreateAsync(courseId, new(request.ClientId, request.Code, request.Name, request.StudyYear, request.AcademicYear, request.UnitIds), ct));
        return result.Replay ? Ok(result) : CreatedAtAction(nameof(Get), new { courseId, id = result.Id }, result);
    }
    [HttpPut("{id:int}")]
    public async Task<AcademicClassResponse> Update(int courseId, int id, UpdateAcademicClassRequest request, CancellationToken ct) => Map(await service.UpdateAsync(courseId, id, new(request.RowVersion, request.Code, request.Name, request.UnitIds), ct));
    [HttpPost("{id:int}/archive")]
    public async Task<AcademicClassResponse> Archive(int courseId, int id, AcademicClassStateRequest request, CancellationToken ct) => Map(await service.ArchiveAsync(courseId, id, request.RowVersion, ct));
    [HttpPost("{id:int}/restore")]
    public async Task<AcademicClassResponse> Restore(int courseId, int id, AcademicClassStateRequest request, CancellationToken ct) => Map(await service.RestoreAsync(courseId, id, request.RowVersion, ct));
    private static AcademicClassResponse Map(AcademicClassResult x) => new(x.Id, x.InstitutionId, x.CourseId, x.ClientId, x.Code, x.Name, x.StudyYear, x.AcademicYear, x.IsActive, x.RowVersion, x.CreatedAt, x.UpdatedAt, x.Units.Select(u => new AcademicClassUnitResponse(u.UnitId, u.Code, u.Name, u.StudyYear, u.IsActive)).ToArray(), x.Replay);
}
