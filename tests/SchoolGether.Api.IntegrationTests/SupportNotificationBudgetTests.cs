using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolGether.Api.Support;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.IntegrationTests;

[Collection("Multipart admission")]
public sealed class SupportNotificationBudgetTests
{
    [Fact]
    public async Task HeldGatesShareOneBudget_AndAreNeverReleasedByUnacquiredWaiters()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        var registry = new SupportConnectionRegistry(); var clock = new ManualClock();
        var first = Blocked(registry, "first", clock); var second = Blocked(registry, "second", clock);
        var notifier = new SupportNotifier(registry, factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Services.GetRequiredService<IHubContext<SupportHub>>(), clock, NullLogger<SupportNotifier>.Instance);
        var notification = notifier.ChangedAsync(1, false, CancellationToken.None);
        try
        {
            Assert.False(notification.IsCompleted);
            clock.Advance(TimeSpan.FromSeconds(5));
            await notification.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(0, first.Gate.CurrentCount); Assert.Equal(0, second.Gate.CurrentCount);
        }
        finally { first.Gate.Release(); second.Gate.Release(); await notification.WaitAsync(TimeSpan.FromSeconds(2)); }
    }

    [Fact]
    public async Task NotificationBudgetReturnsCommittedHttpWrites_AndReleasesBothMultipartAdmissions()
    {
        var clock = new ManualClock();
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));
        await factory.SeedAsync();
        using var school = configured.CreateClient(); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        var registry = configured.Services.GetRequiredService<SupportConnectionRegistry>(); var blocked = Blocked(registry, "blocked", clock);
        var one = SupportAuthorizationTests.Ticket(school); var two = SupportAuthorizationTests.Ticket(school);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await clock.NextTimerAsync(deadline.Token); await clock.NextTimerAsync(deadline.Token);
            Assert.False(one.IsCompleted); Assert.False(two.IsCompleted);
            Assert.Equal(0, SupportMultipartAdmission.Slots.CurrentCount);
            // The changed rows are already committed while notification waits on the gate.
            using (var scope = configured.Services.CreateScope())
                Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<SchoolGether.Infrastructure.Persistence.SchoolGetherDbContext>().SupportTickets.IgnoreQueryFilters().CountAsync(deadline.Token));
            clock.Advance(TimeSpan.FromSeconds(5));
            await Task.WhenAll(one, two).WaitAsync(deadline.Token);
            Assert.Equal(2, SupportMultipartAdmission.Slots.CurrentCount); Assert.Equal(0, blocked.Gate.CurrentCount);
            registry.Remove(blocked.Id);
            using var master = configured.CreateClient(); await PlatformAdminTests.Authenticate(master);
            await using var live = new SupportLiveTestClient(configured, master); await live.StartAsync();
            var recovered = await SupportAuthorizationTests.Ticket(school);
            Assert.Equal(recovered.GetProperty("id").GetInt32(), (await live.NextAsync()).GetProperty("ticketId").GetInt32());
        }
        finally { registry.Remove(blocked.Id); blocked.Gate.Release(); await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    private static SupportConnection Blocked(SupportConnectionRegistry registry, string id, TimeProvider clock)
    {
        var connection = new SupportConnection(id, new(1, null, "PlatformAdmin"), clock.GetUtcNow().AddHours(1), true, () => { });
        connection.Gate.Wait(); registry.Add(connection); return connection;
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        private readonly List<ManualTimer> timers = [];
        private readonly Channel<bool> created = Channel.CreateUnbounded<bool>();
        public override DateTimeOffset GetUtcNow() { lock (timers) return now; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (timers) { var timer = new ManualTimer(this, callback, state); timer.Change(dueTime, period); timers.Add(timer); created.Writer.TryWrite(true); return timer; }
        }
        internal async Task NextTimerAsync(CancellationToken ct) => _ = await created.Reader.ReadAsync(ct);
        internal void Advance(TimeSpan time)
        {
            ManualTimer[] due;
            lock (timers) { now += time; due = timers.Where(x => x.Due <= now).ToArray(); foreach (var timer in due) timer.Due = DateTimeOffset.MaxValue; }
            foreach (var timer in due) timer.Fire();
        }
        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            internal DateTimeOffset Due = DateTimeOffset.MaxValue;
            private bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) { lock (clock.timers) { if (disposed) return false; Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.now + dueTime; return true; } }
            internal void Fire() { if (!disposed) callback(state); }
            public void Dispose() { lock (clock.timers) { disposed = true; Due = DateTimeOffset.MaxValue; } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
