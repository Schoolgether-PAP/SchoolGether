using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class BootstrapTests
{
    [Theory]
    [InlineData("abc", "PasswordTooShort")]
    [InlineData("LongEnoughPassword!", "PasswordRequiresDigit")]
    [InlineData("longenoughpassword42!", "PasswordRequiresUpper")]
    [InlineData("LONGENOUGHPASSWORD42!", "PasswordRequiresLower")]
    [InlineData("LongEnoughPassword42", "PasswordRequiresNonAlphanumeric")]
    public async Task RejectedPassword_ReportsReasonAndCreatesNoAccount(string password, string expectedCode)
    {
        await using var factory = new IdentityFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        await db.Database.EnsureCreatedAsync();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IPlatformAdminBootstrap>();

        var exception = await Assert.ThrowsAsync<BootstrapValidationException>(() =>
            bootstrap.CreateAsync("admin@example.test", password, default));

        Assert.Contains(expectedCode, exception.ErrorCodes);
        Assert.DoesNotContain(password, exception.Message);
        Assert.False(await db.Users.AnyAsync());
    }

    [Fact]
    public async Task ValidBootstrap_CreatesOneAdminAndDoesNotReplaceIt()
    {
        await using var factory = new IdentityFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        await db.Database.EnsureCreatedAsync();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IPlatformAdminBootstrap>();

        Assert.True(await bootstrap.CreateAsync("admin@example.test", IdentityFactory.Password, default));
        Assert.False(await bootstrap.CreateAsync("other@example.test", IdentityFactory.Password, default));
        var user = Assert.Single(await db.Users.ToListAsync());
        Assert.True(user.IsPlatformAdmin);
        Assert.Equal("admin@example.test", user.Email);
    }
}
