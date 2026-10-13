using SchoolGether.Api.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class MasterSetupTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("mismatch")]
    [InlineData("school")]
    [InlineData("inactive")]
    [InlineData("second")]
    public async Task LocalPromotion_RefusesInvalidSelectionWithoutChangingAccounts(string scenario)
    {
        await using var factory = new IdentityFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        await db.Database.EnsureCreatedAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var candidate = new ApplicationUser { UserName = "chosen@example.test", Email = "chosen@example.test", Name = "Escolhido", IsPlatformAdmin = scenario != "school" };
        if (scenario == "inactive") candidate.SetPlatformAccess("Escolhido", false, []);
        Assert.True((await users.CreateAsync(candidate, IdentityFactory.Password)).Succeeded);
        if (scenario == "second")
        {
            var master = new ApplicationUser { UserName = "master@example.test", Email = "master@example.test", Name = "Master", IsPlatformAdmin = true };
            master.PromoteMaster();
            Assert.True((await users.CreateAsync(master, IdentityFactory.Password)).Succeeded);
        }
        var id = scenario == "missing" ? 999999 : candidate.Id;
        using var input = new StringReader($"{id}\n{(scenario == "mismatch" ? id + 1 : id)}\n");
        using var output = new StringWriter();
        Assert.False(await RunConsole(factory.Services, true, input, output));
        db.ChangeTracker.Clear();
        Assert.False((await db.Users.SingleAsync(x => x.Id == candidate.Id)).IsMaster);
        Assert.Equal(scenario == "second" ? 1 : 0, await db.Users.CountAsync(x => x.MasterSlot != null));
        Assert.Contains(await db.AuditEntries.ToListAsync(), x => x.Action == "platform.master.promote" && x.Result == "refused");
    }

    [Fact]
    public async Task LocalPromotion_RequiresDevelopmentAndDoesNotReadInputInProduction()
    {
        await using var factory = new IdentityFactory();
        using var output = new StringWriter();
        Assert.False(await RunConsole(factory.Services, false, new UnreadableInput(), output));
        Assert.Contains("Development", output.ToString());
    }

    [Fact]
    public async Task LocalPromotion_PromotesOnlyChosenExistingGlobalAndPreservesCredentials()
    {
        await using var factory = new IdentityFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        await db.Database.EnsureCreatedAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var first = new ApplicationUser { UserName = "first@example.test", Email = "first@example.test", Name = "Primeiro", IsPlatformAdmin = true };
        var chosen = new ApplicationUser { UserName = "chosen@example.test", Email = "chosen@example.test", Name = "Escolhido", IsPlatformAdmin = true };
        Assert.True((await users.CreateAsync(first, IdentityFactory.Password)).Succeeded);
        Assert.True((await users.CreateAsync(chosen, IdentityFactory.Password)).Succeeded);
        var hash = chosen.PasswordHash; var stamp = chosen.SecurityStamp;
        using var input = new StringReader($"{chosen.Id}\n{chosen.Id}\n");
        using var output = new StringWriter();
        Assert.True(await RunConsole(factory.Services, true, input, output));
        db.ChangeTracker.Clear();
        var persisted = await db.Users.SingleAsync(x => x.Id == chosen.Id);
        Assert.True(persisted.IsMaster); Assert.Equal(hash, persisted.PasswordHash); Assert.Equal(stamp, persisted.SecurityStamp);
        Assert.False((await db.Users.SingleAsync(x => x.Id == first.Id)).IsMaster);
        Assert.Equal(2, await db.Users.CountAsync());
        var audit = Assert.Single(await db.AuditEntries.ToListAsync());
        Assert.Equal("platform.master.promote", audit.Action); Assert.Equal("success", audit.Result);
        Assert.Equal(chosen.Id.ToString(), audit.Target);
        Assert.Equal(DateTimeKind.Utc, audit.CreatedAt.Kind);
        // O índice protege também contra duas promoções concorrentes após ambas lerem «sem master».
        first = await db.Users.SingleAsync(x => x.Id == first.Id); first.PromoteMaster();
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static async Task<bool> RunConsole(IServiceProvider services, bool development, TextReader input, TextWriter output)
        => await MasterSetupConsole.RunAsync(services, development, input, output, CancellationToken.None);

    private sealed class UnreadableInput : TextReader
    {
        public override string? ReadLine() => throw new InvalidOperationException("Não deve ler em produção.");
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Não deve ler em produção.");
    }
}
