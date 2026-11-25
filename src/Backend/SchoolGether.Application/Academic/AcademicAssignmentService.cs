using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Academic;
namespace SchoolGether.Application.Academic;

public sealed class AcademicAssignmentService(AcademicAuthorization authorization,IAcademicAssignmentStore store,IInstitutionTeacherDirectory teachers,IAssignmentUnitOfWork work,IAuditWriter audit,TimeProvider clock)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private static void Validate(int page,int pageSize,string? search)
    {if(page<1||pageSize is <1 or >100||(long)(page-1)*pageSize>int.MaxValue||search?.Length>150)throw new AcademicException("InvalidAssignmentInput");}
    public async Task<AcademicAssignmentPage> ListAsync(int courseId,int classId,AssignmentQuery query,CancellationToken ct)
    {
        var scope=await authorization.RequireAssignmentAsync("read",courseId,classId,ct:ct);Validate(query.Page,query.PageSize,query.Search);
        if(query.Status is not("Active" or "Cancelled"))throw new AcademicException("InvalidAssignmentInput");
        await Parents(scope,ct);var ids=string.IsNullOrWhiteSpace(query.Search)?null:await teachers.SearchIdsAsync(scope.InstitutionId,query.Search,ct);
        var rows=await store.ListAsync(scope,query,ids,ct);var descriptions=await teachers.FindManyAsync(scope.InstitutionId,rows.Items.Select(x=>x.TeacherId).Distinct().ToArray(),ct);
        var units=await store.DescribeUnitsAsync(scope,rows.Items.Select(x=>x.UnitId).Distinct().ToArray(),ct);
        return new(rows.Items.Select(x=>AcademicAssignmentResult.From(x,descriptions.SingleOrDefault(s=>s.UserId==x.TeacherId)) with {Unit=units.SingleOrDefault(u=>u.Id==x.UnitId)}).ToArray(),rows.Total,query.Page,query.PageSize);
    }
    public async Task<AcademicAssignmentResult> GetAsync(int courseId,int classId,int id,CancellationToken ct)
    {var scope=await authorization.RequireAssignmentAsync("read",courseId,classId,id,ct);await Parents(scope,ct);return await Describe(scope,await Value(scope,id,ct),false,ct);}
    public async Task<TeacherDirectoryPage> EligibleTeachersAsync(int courseId,int classId,TeacherQuery query,CancellationToken ct)
    {
        var scope=await authorization.RequireAssignmentAsync("read",courseId,classId,ct:ct);Validate(query.Page,query.PageSize,query.Search);var parents=await Parents(scope,ct);RequireActive(parents.Course,parents.Class);
        return await teachers.ListAsync(scope.InstitutionId,query.Page,query.PageSize,query.Search,await store.ExcludedTeachersAsync(scope,parents.Class.AcademicYear,ct),ct);
    }
    public async Task<AcademicAssignmentResult> CreateAsync(int courseId,int classId,CreateAssignment request,CancellationToken ct)
    {
        var scope=await authorization.RequireAssignmentAsync("create",courseId,classId,ct:ct);
        AcademicAssignment? initial=null;
        try
        {
            var saved=await work.ExecuteAsync(scope,async token=>
            {
                var parents=await Parents(scope,token);
                initial=AcademicAssignment.Create(scope.InstitutionId,courseId,classId,request.TeacherId,request.UnitId,parents.Class.StudyYear,parents.Class.AcademicYear,scope.ActorId,request.ClientId,Now);
                var existing=await store.FindByClientIdAsync(scope,request.ClientId,token);
                if(existing is not null){Replay(existing,initial);return(Value:existing,Replay:true);}
                RequireActive(parents.Course,parents.Class);var teacher=await Eligible(scope,request.TeacherId,token);
                if(!await store.IsSelectedUnitAsync(scope,request.UnitId,token))throw new AcademicException("CourseUnitNotFound");
                store.Bind(scope,parents.Course,parents.Class,initial,teacher);store.Add(scope,initial);
                parents.Course.TouchEnrolments(parents.Course.RowVersion,Now);parents.Class.TouchEnrolments(parents.Class.RowVersion,Now);Append(scope,initial);
                return(Value:initial,Replay:false);
            },ct);
            return await Describe(scope,saved.Value,saved.Replay,ct);
        }
        catch(AcademicException ex)when(initial is not null&&ex.Code is "AssignmentConflict" or "VersionConflict")
        {
            // O UoW já reverteu e limpou as entidades. Reconsulta permite recuperar a criação concorrente matching.
            await Parents(scope,ct);var existing=await store.FindByClientIdAsync(scope,request.ClientId,ct);if(existing is null)throw;
            Replay(existing,initial);return await Describe(scope,existing,true,ct);
        }
    }
    public Task<AcademicAssignmentResult> CancelAsync(int courseId,int classId,int id,long version,CancellationToken ct)=>Change(courseId,classId,id,version,"cancel",ct);
    public Task<AcademicAssignmentResult> RestoreAsync(int courseId,int classId,int id,long version,CancellationToken ct)=>Change(courseId,classId,id,version,"restore",ct);
    private async Task<AcademicAssignmentResult> Change(int courseId,int classId,int id,long version,string operation,CancellationToken ct)
    {
        var scope=await authorization.RequireAssignmentAsync(operation,courseId,classId,id,ct);
        var value=await work.ExecuteAsync(scope,async token=>
        {
            var parents=await Parents(scope,token);var entity=await Value(scope,id,token);TeacherDirectoryItem? teacher=null;
            if(operation=="restore"){RequireActive(parents.Course,parents.Class);teacher=await Eligible(scope,entity.TeacherId,token);if(!await store.IsSelectedUnitAsync(scope,entity.UnitId,token))throw new AcademicException("CourseUnitNotFound");}
            store.Bind(scope,parents.Course,parents.Class,entity,teacher);
            if(operation=="cancel")entity.Cancel(version,Now);else{entity.Restore(version,Now);parents.Course.TouchEnrolments(parents.Course.RowVersion,Now);}
            parents.Class.TouchEnrolments(parents.Class.RowVersion,Now,operation=="cancel");Append(scope,entity);return entity;
        },ct);
        return await Describe(scope,value,false,ct);
    }
    private async Task<(Course Course,AcademicClass Class)> Parents(AuthorizedAssignmentScope scope,CancellationToken ct)
    {var course=await store.FindCourseAsync(scope,ct)??throw new AcademicException("CourseNotFound");var value=await store.FindClassAsync(scope,ct)??throw new AcademicException("ClassNotFound");return(course,value);}
    private Task<AcademicAssignment?> Find(AuthorizedAssignmentScope scope,int id,CancellationToken ct)=>store.FindAsync(scope,id,ct);
    private async Task<AcademicAssignment> Value(AuthorizedAssignmentScope scope,int id,CancellationToken ct)=>await Find(scope,id,ct)??throw new AcademicException("AssignmentNotFound");
    private static void RequireActive(Course course,AcademicClass value){if(!course.IsActive)throw new AcademicException("CourseArchived");if(!value.IsActive)throw new AcademicException("ClassArchived");}
    private async Task<TeacherDirectoryItem> Eligible(AuthorizedAssignmentScope scope,int id,CancellationToken ct)
    {var teacher=(await teachers.FindManyAsync(scope.InstitutionId,[id],ct)).SingleOrDefault()??throw new AcademicException("TeacherNotFound");if(!teacher.Available)throw new AcademicException("TeacherUnavailable");return teacher;}
    private static void Replay(AcademicAssignment existing,AcademicAssignment initial){if(!existing.IsReplayOf(initial))throw new AcademicException("AssignmentReplayConflict");}
    private async Task<AcademicAssignmentResult> Describe(AuthorizedAssignmentScope scope,AcademicAssignment value,bool replay,CancellationToken ct)
    {
        var teacher=(await teachers.FindManyAsync(scope.InstitutionId,[value.TeacherId],ct)).SingleOrDefault();
        var unit=(await store.DescribeUnitsAsync(scope,[value.UnitId],ct)).SingleOrDefault();
        return AcademicAssignmentResult.From(value,teacher,replay) with {Unit=unit};
    }
    private void Append(AuthorizedAssignmentScope scope,AcademicAssignment value)=>audit.Append(new(new(scope.ActorId,scope.ActorName),"academic.assignment."+scope.Operation,"academic.assignment:"+value.ClientId.ToString("D"),"success","Confirmed",scope.InstitutionId));
}
