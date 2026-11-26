using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Web.Identity;
using SchoolGether.Web.Public;

namespace SchoolGether.Web.Tests;

public sealed class RegistrationWizardTests
{
    [Fact]
    public async Task Payment_and_review_do_not_create_account_until_final_confirmation()
    {
        using var fixture = new Fixture();
        using var wizard = await fixture.Open();
        for (var i = 0; i < 4; i++) await Call(wizard, "ContinueAsync");
        Assert.Equal(0, fixture.Registrations);
        Assert.Equal(4, Get<int>(wizard, "step"));
        await Call(wizard, "ContinueAsync");
        Assert.Equal(1, fixture.Registrations);
        using var body = JsonDocument.Parse(fixture.RegistrationBody!);
        Assert.Equal("secretaria@example.test", body.RootElement.GetProperty("details").GetProperty("contactEmail").GetString());
        Assert.False(body.RootElement.TryGetProperty("paymentMethod", out _));
        Assert.DoesNotContain("card", fixture.RegistrationBody!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_contact_blocks_institution_step_and_keeps_entered_values()
    {
        using var fixture = new Fixture();
        using var wizard = await fixture.Open();
        Set(wizard, "step", 1); Set(wizard, "contactEmail", "invalid");
        await Call(wizard, "ContinueAsync");
        Assert.Equal(1, Get<int>(wizard, "step"));
        Assert.Equal("contactEmail", Get<string>(wizard, "invalidField"));
        Assert.NotNull(Get<string?>(wizard, "message"));
        Assert.Equal("Escola de verificação", Get<string>(wizard, "institutionName"));
        Assert.Equal(0, fixture.Registrations);
    }

    private static void Set(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(value, data);
    private static T Get<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    private static async Task Call(object value, string name)
    {
        if (value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(value, null) is Task task) await task;
    }

    private sealed class Fixture : HttpMessageHandler
    {
        private readonly ServiceProvider services;
        public int Registrations;
        public string? RegistrationBody;
        public Fixture()
        {
            var registrations = new ServiceCollection();
            registrations.AddMemoryIdentitySession("https://example.test/", false);
            registrations.AddScoped(_ => new HttpClient(this, false) { BaseAddress = new Uri("https://example.test/") });
            registrations.AddSingleton<NavigationManager>(new TestNavigation());
            services = registrations.BuildServiceProvider();
        }
        public async Task<RegistrationWizard> Open()
        {
            var wizard = new RegistrationWizard();
            foreach (var property in typeof(RegistrationWizard).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (property.GetCustomAttribute<InjectAttribute>() is not null) property.SetValue(wizard, services.GetRequiredService(property.PropertyType));
            await Call(wizard, "OnInitializedAsync");
            Set(wizard, "planId", "essential"); Set(wizard, "ownerName", "Responsável"); Set(wizard, "email", "owner@example.test");
            Set(wizard, "password", "Strong-Test-Password-123!"); Set(wizard, "confirmation", "Strong-Test-Password-123!");
            Set(wizard, "institutionName", "Escola de verificação"); Set(wizard, "institutionType", "School");
            Set(wizard, "academicYear", "2026/2027"); Set(wizard, "accepted", true); Set(wizard, "authorized", true);
            Set(wizard, "contactEmail", "secretaria@example.test"); Set(wizard, "contactRole", "IT"); Set(wizard, "expectedTeachers", 10);
            Set(wizard, "paymentMethod", "Reference");
            return wizard;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/plans")) return new(HttpStatusCode.OK) {
                Content = JsonContent.Create(new { version = "test-v1", registrationEnabled = true, plans = new[] { new { id = "essential", name = "Essencial", price = 1750, currency = "EUR", accountLimit = 20, billingPeriod = "Annual", capacityUnit = "Teachers" } } })
            };
            if (request.RequestUri.AbsolutePath.Contains("/onboarding/")) {
                Registrations++; RegistrationBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(new { institutionId = 3, ownerId = 8, email = "owner@example.test" }) };
            }
            // Login unavailable after a successful registration must not resubmit the signup.
            return new(HttpStatusCode.ServiceUnavailable);
        }
        protected override void Dispose(bool disposing) { if (disposing) services.Dispose(); base.Dispose(disposing); }
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.test/", "https://example.test/?view=aderir");
    }
}
