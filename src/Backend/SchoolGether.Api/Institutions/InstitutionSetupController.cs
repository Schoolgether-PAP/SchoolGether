using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Institutions;
using SchoolGether.Contracts.Billing;
using SchoolGether.Contracts.Institutions;

namespace SchoolGether.Api.Institutions;

[ApiController, Authorize(Roles = "InstitutionAdmin")]
[Route("api/v1/institutions/current")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InstitutionSetupController(InstitutionSetupService setup, IStringLocalizer<ApiMessages> messages) : ControllerBase
{
    [HttpGet("setup")]
    public async Task<InstitutionSetupResponse> Get(CancellationToken cancellationToken) => Map(await setup.GetAsync(cancellationToken));

    [HttpPut("setup")]
    public async Task<InstitutionSetupResponse> Save(SaveInstitutionSetupRequest request, CancellationToken cancellationToken) =>
        Map(await setup.SaveAsync(new SaveInstitutionSetup(request.Name, request.InstitutionType, request.Locality, request.AcademicYear, request.RowVersion), cancellationToken));

    [HttpGet("subscription")]
    public async Task<InstitutionSubscriptionResponse> Subscription(CancellationToken cancellationToken)
    {
        var value = await setup.GetSubscriptionAsync(cancellationToken);
        var plan = value.Plan;
        return new InstitutionSubscriptionResponse(value.Status, plan is null ? null :
            new PlanResponse(plan.Id, messages[plan.NameKey], plan.MonthlyPrice, plan.Currency, plan.AccountLimit), value.CatalogVersion, value.UsedAccounts);
    }
    private static InstitutionSetupResponse Map(InstitutionSetup value) => new(value.InstitutionId, value.Name, value.InstitutionType,
        value.Locality, value.AcademicYear, value.IsComplete, value.RowVersion, value.SuggestedAcademicYear);
}
