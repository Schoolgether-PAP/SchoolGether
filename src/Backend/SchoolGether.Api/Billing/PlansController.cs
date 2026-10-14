using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Billing;
using SchoolGether.Contracts.Billing;

namespace SchoolGether.Api.Billing;

[ApiController]
[AllowAnonymous]
[Route("api/v1/billing/plans")]
public sealed class PlansController(PlanCatalog catalog, IConfiguration configuration,
    IStringLocalizer<ApiMessages> messages) : ControllerBase
{
    [HttpGet]
    public ActionResult<PlanCatalogResponse> GetPlans() => Ok(new PlanCatalogResponse(
        catalog.Version,
        configuration.GetValue<bool>("Onboarding:Enabled"),
        catalog.GetPlans().Select(plan => new PlanResponse(
            plan.Id, messages[plan.NameKey], plan.MonthlyPrice, plan.Currency, plan.AccountLimit)).ToArray()));
}
