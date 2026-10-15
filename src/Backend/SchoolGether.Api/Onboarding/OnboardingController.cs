using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Onboarding;
using SchoolGether.Contracts.Onboarding;

namespace SchoolGether.Api.Onboarding;

[ApiController]
[AllowAnonymous]
[Route("api/v1/onboarding/institutions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OnboardingController(OnboardingService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RegistrationResponse>> Register(RegisterInstitutionRequest request, CancellationToken cancellationToken)
    {
        // AllowAnonymous não deve transformar uma credencial inválida em autorização anónima.
        var authentication = await HttpContext.AuthenticateAsync();
        if (authentication.Failure is not null) return Unauthorized();
        var created = await service.RegisterAsync(new RegisterInstitution(request.PlanId, request.CatalogVersion,
            request.OwnerName, request.Email, request.Password, request.InstitutionName, request.InstitutionType,
            request.Locality, request.AcceptDemonstration), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new RegistrationResponse(created.InstitutionId, created.OwnerId, created.Email));
    }
}
