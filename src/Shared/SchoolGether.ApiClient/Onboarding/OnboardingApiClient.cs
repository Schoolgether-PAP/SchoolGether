using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Onboarding;
namespace SchoolGether.ApiClient.Onboarding;
public sealed class OnboardingApiClient(HttpClient http)
{
    public async Task<RegistrationResponse> RegisterAsync(RegisterInstitutionRequest registration, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/onboarding/institutions") { Content = JsonContent.Create(registration) };
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponseReader.ReadAsync<RegistrationResponse>(response, cancellationToken);
    }
}
