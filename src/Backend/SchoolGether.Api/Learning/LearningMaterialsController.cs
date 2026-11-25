using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Learning;
using SchoolGether.Contracts.Learning;
using SchoolGether.Domain.Learning;
namespace SchoolGether.Api.Learning;
[ApiController, Authorize, Route("api/v1/learning/materials")]
public sealed class LearningMaterialsController(LearningMaterialService service) : ControllerBase
{
    [HttpGet] public async Task<LearningMaterialPageResponse> List([FromQuery] string academicYear, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) { var result = await service.ListAsync(academicYear, page, pageSize, ct); return new(result.Items.Select(Map).ToArray(), result.Total, page, pageSize); }
    [HttpPost] public async Task<IActionResult> Create(CreateLearningMaterialRequest r, CancellationToken ct) => StatusCode(201, Map(await service.CreateAsync(new(r.ClientId, r.CourseId, r.ClassId, r.UnitId, r.AcademicYear, r.Title, r.Content, r.Url, r.IsPublished), ct)));
    [HttpPut("{id:int}")] public async Task<LearningMaterialResponse> Update(int id, UpdateLearningMaterialRequest r, CancellationToken ct) => Map(await service.ChangeAsync(id, r.RowVersion, new(r.Title, r.Content, r.Url, r.IsPublished), ct));
    [HttpPost("{id:int}/archive")] public async Task<LearningMaterialResponse> Archive(int id, LearningMaterialStateRequest r, CancellationToken ct) => Map(await service.ChangeAsync(id, r.RowVersion, null, ct));
    static LearningMaterialResponse Map(LearningMaterial p) => new(p.Id, p.ClientId, p.CourseId, p.ClassId, p.UnitId, p.AcademicYear, p.Title, p.Content, p.Url, p.IsPublished, p.IsArchived, p.RowVersion);
}
