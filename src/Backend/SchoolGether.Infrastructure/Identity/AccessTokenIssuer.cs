using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using SchoolGether.Application.Identity;

namespace SchoolGether.Infrastructure.Identity;

internal sealed class AccessTokenIssuer(TokenSigningKey key) : IAccessTokenIssuer
{
    public AccessToken Issue(Account account, DateTime now)
    {
        var expires = now.AddMinutes(10);
        var claims = new List<Claim> {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", account.Role.ToString()) };
        if (account.InstitutionId.HasValue)
            claims.Add(new("institution_id", account.InstitutionId.Value.ToString(CultureInfo.InvariantCulture)));
        var token = new JwtSecurityToken(TokenSigningKey.Issuer, TokenSigningKey.Audience, claims,
            now, expires, new SigningCredentials(key.Key, SecurityAlgorithms.RsaSha256));
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
