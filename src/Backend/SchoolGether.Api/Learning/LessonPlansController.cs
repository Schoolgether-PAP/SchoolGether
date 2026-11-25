using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Learning;
using SchoolGether.Contracts.Learning;
using SchoolGether.Domain.Learning;
namespace SchoolGether.Api.Learning;
[ApiController, Authorize, Route("api/v1/learning/lesson-plans")]
public sealed class LessonPlansController(LessonPlanService service) : ControllerBase
{
    [HttpGet] public async Task<LessonPlanPageResponse> List([FromQuery] string academicYear, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) { var result = await service.ListAsync(academicYear, page, pageSize, ct); return new(result.Items.Select(Map).ToArray(), result.Total, page, pageSize); }
    [HttpPost] public async Task<IActionResult> Create(CreateLessonPlanRequest r, CancellationToken ct) => StatusCode(201, Map(await service.CreateAsync(new(r.ClientId, r.CourseId, r.ClassId, r.UnitId, r.AcademicYear, r.Title, r.Date, r.Objectives, r.Content, r.Resources), ct)));
    [HttpPut("{id:int}")] public async Task<LessonPlanResponse> Update(int id, UpdateLessonPlanRequest r, CancellationToken ct) => Map(await service.ChangeAsync(id, r.RowVersion, new(r.Title, r.Date, r.Objectives, r.Content, r.Resources), ct));
    [HttpPost("{id:int}/archive")] public async Task<LessonPlanResponse> Archive(int id, LessonPlanStateRequest r, CancellationToken ct) => Map(await service.ChangeAsync(id, r.RowVersion, null, ct));
    static LessonPlanResponse Map(LessonPlan p) => new(p.Id, p.ClientId, p.CourseId, p.ClassId, p.UnitId, p.AcademicYear, p.Title, p.Date, p.Objectives, p.Content, p.Resources, p.IsArchived, p.RowVersion);
}
