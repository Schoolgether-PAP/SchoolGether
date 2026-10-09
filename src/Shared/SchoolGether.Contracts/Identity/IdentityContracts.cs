using System.ComponentModel.DataAnnotations;

namespace SchoolGether.Contracts.Identity;

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 1)] string Password);
public sealed record RefreshRequest([Required, StringLength(64, MinimumLength = 64)] string RefreshToken);
public sealed record CreateUserRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required, StringLength(150)] string Name,
    [Required, RegularExpression("^(InstitutionAdmin|Teacher|Student)$")] string Role,
    [Range(1, int.MaxValue)] int? InstitutionId);
public sealed record AccountResponse(int Id, string Email, string Name, string Role, int? InstitutionId);
public sealed record TokenResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken,
    DateTime RefreshTokenExpiresAt, AccountResponse Account);
public sealed record MemberPageResponse(IReadOnlyList<AccountResponse> Items, int Total, int Page, int PageSize);
