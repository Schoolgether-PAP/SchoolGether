using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Identity;
using SchoolGether.Contracts.Academic;
namespace SchoolGether.Api.Academic;
[ApiController,Authorize,Route("api/v1/academic/courses/{courseId:int}/classes/{classId:int}/enrolments")]
public sealed class AcademicEnrolmentController(AcademicEnrolmentService service):ControllerBase
{
    [HttpGet]
    public async Task<AcademicEnrolmentPageResponse> List(int courseId,int classId,[FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? search=null,[FromQuery]string status="Active",CancellationToken ct=default)
    {var x=await service.ListAsync(courseId,classId,new(page,pageSize,search,status),ct);return new(x.Items.Select(Map).ToArray(),x.Total,x.Page,x.PageSize);}
    [HttpGet("eligible-students")]
    public async Task<AcademicStudentPageResponse> Eligible(int courseId,int classId,[FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? search=null,CancellationToken ct=default)
    {var x=await service.EligibleStudentsAsync(courseId,classId,new(page,pageSize,search),ct);return new(x.Items.Select(Student).ToArray(),x.Total,x.Page,x.PageSize);}
    [HttpGet("{id:int}")]
    public async Task<AcademicEnrolmentResponse> Get(int courseId,int classId,int id,CancellationToken ct)=>Map(await service.GetAsync(courseId,classId,id,ct));
    [HttpPost]
    public async Task<IActionResult> Create(int courseId,int classId,CreateAcademicEnrolmentRequest request,CancellationToken ct)
    {var x=Map(await service.CreateAsync(courseId,classId,new(request.ClientId,request.StudentId),ct));return x.Replay?Ok(x):CreatedAtAction(nameof(Get),new {courseId,classId,id=x.Id},x);}
    [HttpPost("{id:int}/cancel")]
    public async Task<AcademicEnrolmentResponse> Cancel(int courseId,int classId,int id,AcademicEnrolmentStateRequest request,CancellationToken ct)=>Map(await service.CancelAsync(courseId,classId,id,request.RowVersion,ct));
    [HttpPost("{id:int}/restore")]
    public async Task<AcademicEnrolmentResponse> Restore(int courseId,int classId,int id,AcademicEnrolmentStateRequest request,CancellationToken ct)=>Map(await service.RestoreAsync(courseId,classId,id,request.RowVersion,ct));
    private static AcademicStudentResponse Student(StudentDirectoryItem x)=>new(x.UserId,x.Name,x.Email,x.Available);
    private static AcademicEnrolmentResponse Map(AcademicEnrolmentResult x)=>new(x.Id,x.InstitutionId,x.CourseId,x.ClassId,x.StudentId,x.ClientId,x.StudyYear,x.AcademicYear,x.IsActive,x.RowVersion,x.CreatedAt,x.UpdatedAt,x.Student is null?null:Student(x.Student),x.Replay);
}
