using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Institutions;
using SchoolGether.Contracts.Institutions;

namespace SchoolGether.Api.Institutions;

[ApiController, Authorize(Roles = "PlatformAdmin")]
[Route("api/v1/institutions")]
public sealed class InstitutionsController(InstitutionService institutions) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<InstitutionResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InstitutionResponse>> Create(CreateInstitutionRequest request, CancellationToken cancellationToken)
    {
        var id = await institutions.CreateAsync(request.Name, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new InstitutionResponse(id, request.Name.Trim()));
    }
}
