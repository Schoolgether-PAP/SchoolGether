using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace SchoolGether.Infrastructure.Identity;

public sealed class TokenSigningKey : IDisposable
{
    private readonly RSA rsa = RSA.Create();
    public RsaSecurityKey Key { get; }
    public const string Issuer = "SchoolGether";
    public const string Audience = "SchoolGether.Clients";

    public TokenSigningKey(IConfiguration configuration)
    {
        var pem = configuration["Jwt:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(pem))
            throw new InvalidOperationException("Configure Jwt:PrivateKeyPem em user-secrets. Consulte docs/identity.md.");
        rsa.ImportFromPem(pem);
        if (rsa.KeySize < 2048) throw new InvalidOperationException("JwtKeyTooSmall");
        // Exportar a parte privada confirma que esta chave também permite emitir tokens.
        rsa.ExportParameters(true);
        Key = new RsaSecurityKey(rsa);
    }

    public void Dispose() => rsa.Dispose();
}
