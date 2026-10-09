using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SchoolGether.Application.Identity;
using SchoolGether.Contracts.Identity;

namespace SchoolGether.Api.Identity;

[ApiController]
[Route("api/v1/identity")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IdentityController(IdentityService identity) : ControllerBase
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("identity")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await identity.LoginAsync(request.Email, request.Password, cancellationToken)));

    [AllowAnonymous, HttpPost("refresh"), EnableRateLimiting("identity")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await identity.RefreshAsync(request.RefreshToken, cancellationToken)));

    [AllowAnonymous, HttpPost("logout"), EnableRateLimiting("identity")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await identity.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<AccountResponse>> Me(CancellationToken cancellationToken) =>
        Ok(Map(await identity.GetMeAsync(cancellationToken)));

    [Authorize(Roles = "PlatformAdmin,InstitutionAdmin"), HttpPost("users")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AccountResponse>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var account = await identity.CreateAccountAsync(CreateAccount.FromRoleName(request.Email, request.Password,
            request.Name, request.Role, request.InstitutionId), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, Map(account));
    }

    [Authorize(Roles = "InstitutionAdmin"), HttpGet("users")]
    public async Task<ActionResult<MemberPageResponse>> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await identity.ListMembersAsync(page, pageSize, cancellationToken);
        return Ok(new MemberPageResponse(result.Items.Select(Map).ToArray(), result.Total, result.Page, result.PageSize));
    }

    private static AccountResponse Map(Account account) => new(account.Id, account.Email, account.Name,
        account.Role.ToString(), account.InstitutionId);
    private static TokenResponse Map(Tokens tokens) => new(tokens.Access.Value, tokens.Access.ExpiresAt,
        tokens.Refresh, tokens.RefreshExpiresAt, Map(tokens.Account));
}
