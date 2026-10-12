using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SchoolGether.Application.Institutions;
using SchoolGether.Contracts.Identity;

namespace SchoolGether.Api.IntegrationTests;

public sealed class InstitutionAuthenticationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authentication_WhenDirectoryRejectsInstitution_RejectsOnlyItsMembers(bool refresh)
    {
        var directory = new InstitutionAvailability();
        await using var factory = new IdentityFactory(directory);
        await factory.SeedAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var tokens = new Dictionary<string, TokenResponse>();
        foreach (var email in new[] { "student-a@example.test", "student-b@example.test" })
        {
            using var login = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            tokens[email] = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        }

        // A linha continua na base; a decisão pertence ao módulo Institutions.
        directory.UnavailableInstitutionId = factory.InstitutionA;
        foreach (var (email, token) in tokens)
        {
            using var response = refresh
                ? await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(token.RefreshToken))
                : await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password));
            Assert.Equal(token.Account.InstitutionId == factory.InstitutionA
                ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, response.StatusCode);
        }
    }

    private sealed class InstitutionAvailability : IInstitutionDirectory
    {
        public int? UnavailableInstitutionId { get; set; }
        public Task<bool> ExistsAsync(int institutionId, CancellationToken cancellationToken) =>
            Task.FromResult(institutionId != UnavailableInstitutionId);
        public Task<int> CreateAsync(string name, int createdBy, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
