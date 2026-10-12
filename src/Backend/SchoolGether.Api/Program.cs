using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using SchoolGether.Api;
using SchoolGether.Api.Diagnostics;
using SchoolGether.Api.Identity;
using SchoolGether.Infrastructure.Persistence;

var bootstrap = args.Contains("--bootstrap-admin");
var setupKey = args.Contains("--setup-identity-key");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not ("--bootstrap-admin" or "--setup-identity-key")).ToArray());

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var messages = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiMessages>>();
        foreach (var entry in context.ModelState.Values)
            if (entry.Errors.Count > 0)
            {
                entry.Errors.Clear();
                entry.Errors.Add(messages["InvalidField"]);
            }
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = messages["ValidationFailed"],
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        var result = new BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    };
});
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>(
    "mysql", failureStatus: HealthStatus.Unhealthy, tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddApiIdentity();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var messages = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiMessages>>();
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Title = context.ProblemDetails.Status switch
        {
            StatusCodes.Status404NotFound => messages["NotFound"],
            StatusCodes.Status405MethodNotAllowed => messages["MethodNotAllowed"],
            StatusCodes.Status500InternalServerError => messages["UnexpectedError"],
            StatusCodes.Status429TooManyRequests => messages["TooManyRequests"],
            StatusCodes.Status401Unauthorized when context.ProblemDetails.Title is null or "Unauthorized" => messages["InvalidSession"],
            StatusCodes.Status403Forbidden when context.ProblemDetails.Title is null or "Forbidden" => messages["Forbidden"],
            _ => context.ProblemDetails.Title
        };
        if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            context.ProblemDetails.Detail = null;
        }
    };
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (bootstrap || setupKey)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("O assistente inicial só está disponível em Development.");
    if (setupKey) await IdentitySetupConsole.RunAsync(app.Configuration, app.Environment.ContentRootPath,
        app.Services.GetRequiredService<IStringLocalizer<ApiMessages>>());
    else await BootstrapConsole.RunAsync(app.Services);
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("../openapi/v1.json", "SchoolGether API v1");
        options.DocumentTitle = "SchoolGether API";
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Permite que os testes iniciem a API com WebApplicationFactory.
public partial class Program;
