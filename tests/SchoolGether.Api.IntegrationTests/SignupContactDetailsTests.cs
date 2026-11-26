using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SignupContactDetailsTests
{
    internal static Dictionary<string, object?> Details() => new()
    {
        ["contactEmail"] = " secretaria@example.test ", ["contactRole"] = "IT",
        ["phone"] = "+351 212 345 678", ["address"] = " Rua da Escola, 10 ",
        ["postalCode"] = "2135-100", ["taxNumber"] = "123456789", ["expectedTeachers"] = 18
    };

    [Fact]
    public async Task Signup_contacts_are_saved_with_institution_and_not_exposed_to_another_institution()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var request = OnboardingTests.Request(); request["details"] = Details();
        using var created = await client.PostAsJsonAsync(OnboardingTests.Route, request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await AcademicCourseTests.Login(client, "new-owner@example.test");
        var setup = await AcademicCourseTests.Read(await client.GetAsync("/api/v1/institutions/current/setup"));
        var details = setup.GetProperty("details");
        Assert.Equal("secretaria@example.test", details.GetProperty("contactEmail").GetString());
        Assert.Equal("IT", details.GetProperty("contactRole").GetString());
        Assert.Equal("+351 212 345 678", details.GetProperty("phone").GetString());
        Assert.Equal("Rua da Escola, 10", details.GetProperty("address").GetString());
        Assert.Equal("2135-100", details.GetProperty("postalCode").GetString());
        Assert.Equal("123456789", details.GetProperty("taxNumber").GetString());
        Assert.Equal(18, details.GetProperty("expectedTeachers").GetInt32());
        // Saving the pre-existing setup form must preserve the new contact data.
        await AcademicCourseTests.Read(await client.PutAsJsonAsync("/api/v1/institutions/current/setup", new {
            name = "Escola Nova", institutionType = "School", locality = "Lisboa", academicYear = "2026/2027", rowVersion = setup.GetProperty("rowVersion").GetInt64()
        }));
        setup = await AcademicCourseTests.Read(await client.GetAsync("/api/v1/institutions/current/setup"));
        Assert.Equal("secretaria@example.test", setup.GetProperty("details").GetProperty("contactEmail").GetString());
        await AcademicCourseTests.Login(client, "admin-a@example.test");
        var other = await AcademicCourseTests.Read(await client.GetAsync("/api/v1/institutions/current/setup"));
        Assert.Equal(JsonValueKind.Null, other.GetProperty("details").ValueKind);
        Assert.DoesNotContain("secretaria@example.test", other.ToString());
    }

    [Theory]
    [InlineData("contactEmail", "invalid")]
    [InlineData("contactRole", "Unknown")]
    [InlineData("postalCode", "2135100")]
    [InlineData("taxNumber", "abc123456")]
    [InlineData("phone", "invalid-phone")]
    [InlineData("expectedTeachers", 0)]
    public async Task Invalid_signup_details_do_not_create_an_account(string field, object value)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var request = OnboardingTests.Request(); var details = Details(); details[field] = value; request["details"] = details;
        using var response = await client.PostAsJsonAsync(OnboardingTests.Route, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await OnboardingTests.AssertUnchanged(factory);
    }
}
