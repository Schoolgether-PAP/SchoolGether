using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Identity;
using SchoolGether.Contracts.Academic;
namespace SchoolGether.Api.Academic;
[ApiController,Authorize,Route("api/v1/academic/courses/{courseId:int}/classes/{classId:int}/assignments")]
public sealed class AcademicAssignmentController(AcademicAssignmentService service):ControllerBase
{
    [HttpGet]
    public async Task<AcademicAssignmentPageResponse> List(int courseId,int classId,[FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? search=null,[FromQuery]string status="Active",CancellationToken ct=default)
    {var x=await service.ListAsync(courseId,classId,new(page,pageSize,search,status),ct);return new(x.Items.Select(Map).ToArray(),x.Total,x.Page,x.PageSize);}
    [HttpGet("eligible-teachers")]
    public async Task<AcademicTeacherPageResponse> Eligible(int courseId,int classId,[FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? search=null,CancellationToken ct=default)
    {var x=await service.EligibleTeachersAsync(courseId,classId,new(page,pageSize,search),ct);return new(x.Items.Select(Teacher).ToArray(),x.Total,x.Page,x.PageSize);}
    [HttpGet("{id:int}")]
    public async Task<AcademicAssignmentResponse> Get(int courseId,int classId,int id,CancellationToken ct)=>Map(await service.GetAsync(courseId,classId,id,ct));
    [HttpPost]
    public async Task<IActionResult> Create(int courseId,int classId,CreateAcademicAssignmentRequest request,CancellationToken ct)
    {var x=Map(await service.CreateAsync(courseId,classId,new(request.ClientId,request.TeacherId,request.UnitId),ct));return x.Replay?Ok(x):CreatedAtAction(nameof(Get),new {courseId,classId,id=x.Id},x);}
    [HttpPost("{id:int}/cancel")]
    public async Task<AcademicAssignmentResponse> Cancel(int courseId,int classId,int id,AcademicAssignmentStateRequest request,CancellationToken ct)=>Map(await service.CancelAsync(courseId,classId,id,request.RowVersion,ct));
    [HttpPost("{id:int}/restore")]
    public async Task<AcademicAssignmentResponse> Restore(int courseId,int classId,int id,AcademicAssignmentStateRequest request,CancellationToken ct)=>Map(await service.RestoreAsync(courseId,classId,id,request.RowVersion,ct));
    private static AcademicTeacherResponse Teacher(TeacherDirectoryItem x)=>new(x.UserId,x.Name,x.Email,x.Available);
    private static AcademicAssignmentResponse Map(AcademicAssignmentResult x)=>new(x.Id,x.InstitutionId,x.CourseId,x.ClassId,x.TeacherId,x.UnitId,x.ClientId,x.StudyYear,x.AcademicYear,x.IsActive,x.RowVersion,x.CreatedAt,x.UpdatedAt,x.Teacher is null?null:Teacher(x.Teacher),x.Replay,x.Unit is null?null:new(x.Unit.Id,x.Unit.Code,x.Unit.Name));
}
