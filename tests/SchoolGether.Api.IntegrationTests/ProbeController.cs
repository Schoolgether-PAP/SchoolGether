using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace SchoolGether.Api.IntegrationTests;

[ApiController]
[Route("api/v1/test")]
public sealed class ProbeController : ControllerBase
{
    [HttpGet("failure")]
    public IActionResult Failure() => throw new InvalidOperationException("private-test-marker");

    [HttpPost("validation")]
    public IActionResult Validate(ProbeRequest request) => Ok();
}

public sealed record ProbeRequest([Range(1, 100)] int Value);
