using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Academic;
using SchoolGether.Contracts.Learning;

namespace SchoolGether.Api.Learning;

[ApiController, Authorize, Route("api/v1/learning/contexts")]
public sealed class LearningContextsController(ILearningContextDirectory directory) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<LearningContextResponse>> List([FromQuery] string academicYear, CancellationToken ct)
    {
        var contexts = await directory.ListAsync(academicYear, ct);
        return contexts.Select(x => new LearningContextResponse(x.CourseId, x.CourseName, x.ClassId,
            x.ClassName, x.UnitId, x.UnitName, x.StudyYear, x.AcademicYear)).ToArray();
    }
}
