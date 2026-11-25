using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Academic;

namespace SchoolGether.Infrastructure.Academic;

public static class DependencyInjection
{
    public static IServiceCollection AddAcademicModule(this IServiceCollection services) => services
        .AddScoped<ILearningContextDirectory, LearningContextDirectory>().AddScoped<AcademicAssignmentService>().AddScoped<IAcademicAssignmentStore, AcademicAssignmentStore>().AddScoped<IAssignmentUnitOfWork, AssignmentUnitOfWork>()
        .AddScoped<AcademicEnrolmentService>().AddScoped<IAcademicEnrolmentStore, AcademicEnrolmentStore>().AddScoped<IEnrolmentUnitOfWork, EnrolmentUnitOfWork>()
        .AddScoped<AcademicAuthorization>().AddScoped<AcademicCourseService>()
        .AddScoped<AcademicClassService>().AddScoped<IAcademicClassStore, AcademicClassStore>().AddScoped<IClassUnitOfWork, ClassUnitOfWork>()
        .AddScoped<AcademicCourseUnitService>().AddScoped<IAcademicCourseUnitStore, AcademicCourseUnitStore>().AddScoped<ICourseUnitOfWork, CourseUnitOfWork>()
        .AddScoped<IAcademicCourseStore, AcademicCourseStore>().AddScoped<IAcademicUnitOfWork, AcademicUnitOfWork>();
}
