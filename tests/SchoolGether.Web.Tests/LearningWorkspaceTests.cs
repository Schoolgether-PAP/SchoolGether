using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Identity;
using SchoolGether.Web.Identity;
using SchoolGether.Web.Learning;

namespace SchoolGether.Web.Tests;

// Exercise the component handlers through real typed clients and a controlled HTTP boundary.
// No production database, credentials or new UI-test dependency is required.
public sealed class LearningWorkspaceTests
{
    [Theory]
    [InlineData(typeof(TeachingMaterials))]
    [InlineData(typeof(LessonPlans))]
    public async Task Failed_next_page_keeps_the_confirmed_page_and_retry_requests_that_page(Type type)
    {
        using var fixture = new WorkspaceFixture(type);
        await fixture.SignIn();
        var component = await fixture.Open();
        fixture.FailList = true;
        await Call(component, "Next");
        Assert.Equal(1, Get<int>(component, "page"));
        Assert.NotNull(Get<string?>(component, "error"));
        fixture.FailList = false;
        await Call(component, "Next");
        Assert.Equal(2, Get<int>(component, "page"));
        Assert.EndsWith("page=2", fixture.LastListPath);
        ((IDisposable)component).Dispose();
    }

    [Theory]
    [InlineData(typeof(TeachingMaterials))]
    [InlineData(typeof(LessonPlans))]
    public async Task Failed_year_search_keeps_the_previous_confirmed_page_and_year(Type type)
    {
        using var fixture = new WorkspaceFixture(type);
        await fixture.SignIn();
        var component = await fixture.Open();
        await Call(component, "Next");
        Set(component, "year", "2027/2028");
        fixture.FailList = true;
        await Call(component, "Search");
        Assert.Equal(2, Get<int>(component, "page"));
        Assert.Equal("2026/2027", Get<string>(component, "loadedYear"));
        ((IDisposable)component).Dispose();
    }

    [Theory]
    [InlineData(typeof(TeachingMaterials))]
    [InlineData(typeof(LessonPlans))]
    public async Task Lost_create_response_survives_leaving_area_and_reuses_the_same_request(Type type)
    {
        using var fixture = new WorkspaceFixture(type);
        await fixture.SignIn();
        var component = await fixture.Open();
        await Call(component, "New");
        Set(component, "contextKey", "1:2:3");
        Set(component, "title", "Aula preparada");
        fixture.LoseCreateResponse = true;
        await Call(component, "Save");
        var original = Assert.Single(fixture.Creates);
        ((IDisposable)component).Dispose();
        component = await fixture.Open();
        Assert.True(Get<bool>(component, "editing"));
        Assert.Equal("Aula preparada", Get<string>(component, "title"));
        fixture.LoseCreateResponse = false;
        await Call(component, "Save");
        Assert.Equal(2, fixture.Creates.Count);
        Assert.Equal(original, fixture.Creates[1]);
        Assert.False(Get<bool>(component, "editing"));
        ((IDisposable)component).Dispose();
        component = await fixture.Open();
        Assert.False(Get<bool>(component, "editing"));
        ((IDisposable)component).Dispose();
    }

    [Theory]
    [InlineData(typeof(TeachingMaterials))]
    [InlineData(typeof(LessonPlans))]
    public async Task Profile_refresh_preserves_pending_request_but_sign_out_clears_it(Type type)
    {
        using var fixture = new WorkspaceFixture(type);
        await fixture.SignIn();
        var component = await fixture.Open();
        await Call(component, "New");
        Set(component, "contextKey", "1:2:3");
        Set(component, "title", "Pedido pendente");
        fixture.LoseCreateResponse = true;
        await Call(component, "Save");
        ((IDisposable)component).Dispose();
        await fixture.Session.GetProfileAsync();
        component = await fixture.Open();
        Assert.True(Get<bool>(component, "editing"));
        Assert.Equal("Pedido pendente", Get<string>(component, "title"));
        ((IDisposable)component).Dispose();
        await fixture.Session.SignOutAsync();
        await fixture.SignIn();
        component = await fixture.Open();
        Assert.False(Get<bool>(component, "editing"));
        ((IDisposable)component).Dispose();
    }

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target)!;
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static async Task Call(object target, string name)
    {
        var result = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);
        if (result is Task task) await task;
    }

    private sealed class WorkspaceFixture : HttpMessageHandler
    {
        private readonly Type componentType;
        private readonly ServiceProvider services;
        public bool FailList, LoseCreateResponse;
        public string LastListPath = "";
        public List<string> Creates { get; } = [];
        public WorkspaceFixture(Type type)
        {
            componentType = type;
            var registrations = new ServiceCollection();
            registrations.AddMemoryIdentitySession("https://example.test/", false);
            registrations.AddScoped(_ => new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("https://example.test/") });
            services = registrations.BuildServiceProvider();
        }
        public IdentitySession Session => services.GetRequiredService<IdentitySession>();
        public Task SignIn() => services.GetRequiredService<IdentitySession>().SignInAsync(new("teacher@example.test", "test-password"));
        public async Task<object> Open()
        {
            var component = Activator.CreateInstance(componentType)!;
            foreach (var property in componentType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (property.GetCustomAttribute<InjectAttribute>() is not null)
                    property.SetValue(component, services.GetRequiredService(property.PropertyType));
            Set(component, "year", "2026/2027");
            await Call(component, "OnInitializedAsync");
            return component;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var account = new AccountResponse(7, "teacher@example.test", "Professor", "Teacher", 2);
            if (path.EndsWith("/login")) return Json(new TokenResponse("test-token", DateTime.UtcNow.AddHours(1), new string('a', 64), DateTime.UtcNow.AddDays(1), account));
            if (path.EndsWith("/logout")) return new(HttpStatusCode.NoContent);
            if (path.EndsWith("/me")) return Json(account);
            if (path.Contains("/contexts")) return Json(new[] { new { courseId = 1, classId = 2, unitId = 3, className = "12 A", unitName = "Programação", academicYear = "2026/2027" } });
            if (request.Method == HttpMethod.Post)
            {
                Creates.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                if (LoseCreateResponse) throw new HttpRequestException("Response lost after commit");
                // Create handlers only need the acknowledgement before reloading the list.
                using var body = JsonDocument.Parse(Creates[^1]);
                return Json(new { id = 1, clientId = body.RootElement.GetProperty("clientId").GetGuid(), title = "Aula preparada", academicYear = "2026/2027", date = "2026-11-26", rowVersion = 1 });
            }
            LastListPath = path;
            if (FailList) return new(HttpStatusCode.ServiceUnavailable);
            return Json(new { items = Array.Empty<object>(), total = 90, page = 1, pageSize = 30 });
        }
        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
        protected override void Dispose(bool disposing) { if (disposing) services.Dispose(); base.Dispose(disposing); }
    }
}
