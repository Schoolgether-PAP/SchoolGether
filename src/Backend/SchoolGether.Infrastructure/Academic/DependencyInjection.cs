using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Academic;

namespace SchoolGether.Infrastructure.Academic;

public static class DependencyInjection
{
    public static IServiceCollection AddAcademicModule(this IServiceCollection services) => services
        .AddScoped<AcademicAuthorization>().AddScoped<AcademicCourseService>()
        .AddScoped<AcademicCourseUnitService>().AddScoped<IAcademicCourseUnitStore, AcademicCourseUnitStore>().AddScoped<ICourseUnitOfWork, CourseUnitOfWork>()
        .AddScoped<IAcademicCourseStore, AcademicCourseStore>().AddScoped<IAcademicUnitOfWork, AcademicUnitOfWork>();
}
