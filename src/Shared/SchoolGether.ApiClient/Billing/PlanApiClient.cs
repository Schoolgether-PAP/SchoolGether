using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Billing;
namespace SchoolGether.ApiClient.Billing;
public sealed class PlanApiClient(HttpClient http)
{
    public async Task<PlanCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/billing/plans");
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponseReader.ReadAsync<PlanCatalogResponse>(response, cancellationToken);
    }
}
