using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Identity;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Domain.Audit;
using SchoolGether.Infrastructure.Audit;
using SchoolGether.Infrastructure.Onboarding;
using SchoolGether.Domain.Billing;
using SchoolGether.Domain.Support;
using SchoolGether.Infrastructure.Support;
using SchoolGether.Domain.Academic;
using SchoolGether.Infrastructure.Academic;

namespace SchoolGether.Infrastructure.Persistence;

public sealed class SchoolGetherDbContext(DbContextOptions<SchoolGetherDbContext> options, ICurrentAccount? current = null)
    : IdentityUserContext<ApplicationUser, int>(options)
{
    internal OnboardingWriteScope? OnboardingScope { get; set; }
    internal SupportWriteScope? SupportScope { get; set; }
    internal AcademicWriteScope? AcademicScope { get; set; }
    internal CourseUnitWriteScope? CourseUnitScope { get; set; }
    public DbSet<CourseUnit> CourseUnits => Set<CourseUnit>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<SupportAttachment> SupportAttachments => Set<SupportAttachment>();
    public DbSet<SupportStorageUsage> SupportStorageUsages => Set<SupportStorageUsage>();
    private int? AccountId => current?.UserId;
    private string? AccountRoleName => current?.Role;
    public DbSet<InstitutionProfile> InstitutionProfiles => Set<InstitutionProfile>();
    public DbSet<DemonstrationSubscription> Subscriptions => Set<DemonstrationSubscription>();
    private int? InstitutionId => current?.InstitutionId;
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<InstitutionMember> Members => Set<InstitutionMember>();
    public DbSet<RefreshSession> Sessions => Set<RefreshSession>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(AuditAppendOnlyInterceptor.Instance);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolGetherDbContext).Assembly);
        IdentityModel.Configure(modelBuilder);
        // Inclui arquivados para consulta/restauro e reserva permanente dos códigos.
        modelBuilder.Entity<CourseUnit>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && AccountRoleName == "InstitutionAdmin");
        modelBuilder.Entity<Course>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && AccountRoleName == "InstitutionAdmin");
        modelBuilder.Entity<SupportAttachment>().HasQueryFilter(x => x.DeletedAt == null && InstitutionId != null &&
            x.InstitutionId == InstitutionId && SupportMessages.Any(m => m.Id == x.MessageId));
        modelBuilder.Entity<SupportStorageUsage>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId);
        modelBuilder.Entity<SupportTicket>().HasQueryFilter(x => x.DeletedAt == null && InstitutionId != null &&
            x.InstitutionId == InstitutionId && (AccountRoleName == "InstitutionAdmin" || (AccountRoleName == "Teacher" && x.AuthorId == AccountId)));
        modelBuilder.Entity<SupportMessage>().HasQueryFilter(x => x.DeletedAt == null && !x.Internal && InstitutionId != null &&
            x.InstitutionId == InstitutionId && SupportTickets.Any(t => t.Id == x.TicketId));
        modelBuilder.Entity<InstitutionProfile>().HasQueryFilter(x => x.DeletedAt == null && InstitutionId != null && x.InstitutionId == InstitutionId);
        modelBuilder.Entity<DemonstrationSubscription>().HasQueryFilter(x => x.DeletedAt == null && InstitutionId != null && x.InstitutionId == InstitutionId);
        modelBuilder.Entity<InstitutionMember>().HasQueryFilter(member =>
            member.DeletedAt == null && InstitutionId != null && member.InstitutionId == InstitutionId);
    }

    private void ValidateTenantWrites()
    {
        if (ChangeTracker.Entries<AuditEntry>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("AuditAppendOnly");
        // A exclusividade antecede todos os retornos dos escopos existentes.
        if (CourseUnitScope is not null)
        {
            if (AcademicScope is not null || SupportScope is not null || OnboardingScope is not null)
                throw new InvalidOperationException("CourseUnitWriteDenied");
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!CourseUnitScope.Allows(change)) throw new InvalidOperationException("CourseUnitWriteDenied");
            return;
        }
        if (ChangeTracker.Entries<CourseUnit>().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("CourseUnitWriteDenied");
        if (AcademicScope is not null)
        {
            if (SupportScope is not null || OnboardingScope is not null) throw new InvalidOperationException("AcademicWriteDenied");
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!AcademicScope.Allows(change)) throw new InvalidOperationException("AcademicWriteDenied");
            return;
        }
        if (ChangeTracker.Entries<Course>().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("AcademicWriteDenied");
        if (SupportScope is not null)
        {
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!SupportScope.Allows(change)) throw new InvalidOperationException("SupportWriteDenied");
            return;
        }
        if (ChangeTracker.Entries().Any(x => x.Entity is SupportTicket or SupportMessage or SupportAttachment or SupportStorageUsage && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("SupportWriteDenied");
        if (OnboardingScope is not null)
        {
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!OnboardingScope.Allows(change)) throw new InvalidOperationException("OnboardingWriteDenied");
            return;
        }
        foreach (var change in ChangeTracker.Entries().Where(x =>
                     (x.Entity is InstitutionProfile or DemonstrationSubscription) &&
                     x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var tenant = change.Property("InstitutionId");
            if (InstitutionId is null || (int)tenant.CurrentValue! != InstitutionId ||
                (change.State != EntityState.Added && (int)tenant.OriginalValue! != InstitutionId))
                throw new InvalidOperationException("TenantWriteDenied");
        }
        foreach (var entry in ChangeTracker.Entries<InstitutionMember>().Where(x =>
                     x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.State == EntityState.Added && current?.Role == nameof(AccountRole.PlatformAdmin)) continue;
            if (InstitutionId is null || entry.Entity.InstitutionId != InstitutionId ||
                (entry.State != EntityState.Added && entry.Property(x => x.InstitutionId).OriginalValue != InstitutionId))
                throw new InvalidOperationException("TenantWriteDenied");
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();
        var count = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        OnboardingScope?.RecordSavedEntities();
        return count;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites();
        var count = base.SaveChanges(acceptAllChangesOnSuccess);
        OnboardingScope?.RecordSavedEntities();
        return count;
    }
}
