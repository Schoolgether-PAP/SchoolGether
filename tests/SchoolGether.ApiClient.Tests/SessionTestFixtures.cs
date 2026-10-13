using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Identity;

namespace SchoolGether.ApiClient.Tests;

internal sealed record SessionRequest(string Method, string Path, string? Scheme, string? Bearer, string? Body);

internal sealed class SessionTestFixture : IDisposable
{
    public static readonly AccountResponse Profile = new(7, "student@example.test", "Aluno", "Student", 2);
    public static readonly LoginRequest Credentials = new("student@example.test", "test-password");
    public static readonly string FirstRefresh = new('a', 64);
    public static readonly string SecondRefresh = new('b', 64);
    public ManualTimeProvider Clock { get; } = new();
    public IRefreshTokenStore Store { get; }
    public IdentitySession Session { get; }
    public HttpClient Http => http;
    public ConcurrentQueue<SessionRequest> Requests { get; } = new();
    public Func<SessionRequest, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; }
    private readonly HttpClient http;

    public SessionTestFixture(IRefreshTokenStore? store = null)
    {
        Store = store ?? new MemoryRefreshTokenStore();
        Respond = (request, _) => Task.FromResult(request.Path switch
        {
            "/api/v1/identity/login" => Json(Tokens()),
            "/api/v1/identity/refresh" => Json(Tokens("access-2", SecondRefresh)),
            "/api/v1/identity/me" => Json(Profile),
            "/api/v1/identity/logout" => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => throw new InvalidOperationException("Unexpected endpoint")
        });
        http = new HttpClient(new ControlledHandler(async (request, cancellationToken) =>
        {
            var captured = new SessionRequest(request.Method.Method, request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Enqueue(captured);
            return await Respond(captured, cancellationToken);
        })) { BaseAddress = new Uri("https://schoolgether.example.test/") };
        Session = new IdentitySession(new IdentityApiClient(http), Store, Clock);
    }

    public TokenResponse Tokens(string access = "access-1", string? refresh = null) => new(
        access, Clock.GetUtcNow().AddMinutes(5).UtcDateTime, refresh ?? FirstRefresh,
        Clock.GetUtcNow().AddDays(7).UtcDateTime,
        new AccountResponse(7, "student@example.test", "Embedded stale name", "Student", 2));

    public static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    public static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose() => http.Dispose();

    private sealed class ControlledHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now += duration;
}

internal sealed class FailingRefreshTokenStore : IRefreshTokenStore
{
    private readonly MemoryRefreshTokenStore inner = new();
    public Task<string?> ReadAsync(CancellationToken cancellationToken) => inner.ReadAsync(cancellationToken);
    public Task WriteAsync(string? value, CancellationToken cancellationToken)
        => value is null ? inner.WriteAsync(null, cancellationToken) : throw new IOException("Storage unavailable");
}
