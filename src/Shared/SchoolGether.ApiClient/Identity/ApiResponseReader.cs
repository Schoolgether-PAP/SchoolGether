using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolGether.ApiClient.Identity;

internal static class ApiResponseReader
{
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        try
        {
            return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))
                ?? throw new HttpRequestException("EmptyApiResponse", null, response.StatusCode);
        }
        catch (JsonException)
        {
            throw new HttpRequestException("InvalidApiResponse", null, response.StatusCode);
        }
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        string? title = null;
        string? traceId = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    title = ReadString(json.RootElement, "title", 200);
                    traceId = ReadString(json.RootElement, "traceId", 200);
                }
            }
            catch (JsonException)
            {
                // Malformed ProblemDetails must not hide the HTTP status or expose raw content.
            }
        }
        throw new IdentityApiException(response.StatusCode, title, traceId);
    }

    private static string? ReadString(JsonElement root, string name, int maxLength)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return text is { Length: > 0 } && text.Length <= maxLength && !text.Any(char.IsControl) ? text : null;
    }
}
