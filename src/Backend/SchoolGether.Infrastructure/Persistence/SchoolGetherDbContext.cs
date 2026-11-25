using Microsoft.EntityFrameworkCore;
using SchoolGether.Domain.Calendar;
using SchoolGether.Domain.Learning;
using SchoolGether.Infrastructure.Learning;
using SchoolGether.Infrastructure.Calendar;
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
    internal LearningMaterialWriteScope? LearningMaterialScope { get; set; }
    public DbSet<LearningMaterial> LearningMaterials => Set<LearningMaterial>();
    internal LessonPlanWriteScope? LessonPlanScope { get; set; }
    public DbSet<LessonPlan> LessonPlans => Set<LessonPlan>();
    internal CalendarWriteScope? CalendarScope { get; set; }
    public DbSet<SchoolEvent> CalendarEvents => Set<SchoolEvent>();
    internal OnboardingWriteScope? OnboardingScope { get; set; }
    internal SupportWriteScope? SupportScope { get; set; }
    internal AcademicWriteScope? AcademicScope { get; set; }
    internal CourseUnitWriteScope? CourseUnitScope { get; set; }
    internal AssignmentWriteScope? AssignmentScope { get; set; }
    public DbSet<AcademicAssignment> Assignments => Set<AcademicAssignment>();
    internal EnrolmentWriteScope? EnrolmentScope { get; set; }
    public DbSet<AcademicEnrolment> Enrolments => Set<AcademicEnrolment>();
    internal ClassWriteScope? ClassScope { get; set; }
    public DbSet<AcademicClass> AcademicClasses => Set<AcademicClass>();
    public DbSet<AcademicClassUnit> ClassUnits => Set<AcademicClassUnit>();
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
        modelBuilder.Entity<LearningMaterial>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && ((AccountRoleName == "Teacher" && x.TeacherId == AccountId) || (AccountRoleName == "Student" && x.IsPublished)));
        modelBuilder.Entity<LessonPlan>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && AccountRoleName == "Teacher" && x.TeacherId == AccountId);
        modelBuilder.Entity<SchoolEvent>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && (AccountRoleName == "InstitutionAdmin" || ((AccountRoleName == "Teacher" || AccountRoleName == "Student") && (x.Status == "Published" || (AccountRoleName == "Teacher" && x.CreatedBy == AccountId)))));
        modelBuilder.Entity<AcademicAssignment>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && AccountRoleName == "InstitutionAdmin");
        modelBuilder.Entity<AcademicEnrolment>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && AccountRoleName == "InstitutionAdmin");
        // Inclui arquivados para consulta/restauro e reserva permanente dos códigos.
        modelBuilder.Entity<CourseUnit>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && AccountRoleName == "InstitutionAdmin");
        modelBuilder.Entity<AcademicClass>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && AccountRoleName == "InstitutionAdmin");
        modelBuilder.Entity<AcademicClassUnit>().HasQueryFilter(x => InstitutionId != null && x.InstitutionId == InstitutionId && AccountRoleName == "InstitutionAdmin");
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
        if (LearningMaterialScope is not null)
        {
            if (LessonPlanScope is not null || AssignmentScope is not null || EnrolmentScope is not null || CalendarScope is not null || ClassScope is not null || CourseUnitScope is not null || AcademicScope is not null || SupportScope is not null || OnboardingScope is not null) throw new InvalidOperationException("LearningMaterialWriteDenied");
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!LearningMaterialScope.Allows(change)) throw new InvalidOperationException("LearningMaterialWriteDenied");
            return;
        }
        if (ChangeTracker.Entries<LearningMaterial>().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("LearningMaterialWriteDenied");
        if (LessonPlanScope is not null)
        {
            if (AssignmentScope is not null || EnrolmentScope is not null || CalendarScope is not null || ClassScope is not null || CourseUnitScope is not null || AcademicScope is not null || SupportScope is not null || OnboardingScope is not null) throw new InvalidOperationException("LessonPlanWriteDenied");
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!LessonPlanScope.Allows(change)) throw new InvalidOperationException("LessonPlanWriteDenied");
            return;
        }
        if (ChangeTracker.Entries<LessonPlan>().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("LessonPlanWriteDenied");
        if (AssignmentScope is not null)
        {
            if (EnrolmentScope is not null || CalendarScope is not null || ClassScope is not null || CourseUnitScope is not null || AcademicScope is not null || SupportScope is not null || OnboardingScope is not null) throw new InvalidOperationException("AssignmentWriteDenied");
            foreach(var change in ChangeTracker.Entries().Where(x=>x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if(!AssignmentScope.Allows(change))throw new InvalidOperationException("AssignmentWriteDenied");
            return;
        }
        if(ChangeTracker.Entries<AcademicAssignment>().Any(x=>x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))throw new InvalidOperationException("AssignmentWriteDenied");
        if (EnrolmentScope is not null)
        {
            if (CalendarScope is not null || ClassScope is not null || CourseUnitScope is not null || AcademicScope is not null || SupportScope is not null || OnboardingScope is not null) throw new InvalidOperationException("EnrolmentWriteDenied");
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!EnrolmentScope.Allows(change)) throw new InvalidOperationException("EnrolmentWriteDenied");
            return;
        }
        if (ChangeTracker.Entries<AcademicEnrolment>().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("EnrolmentWriteDenied");
        if (CalendarScope is not null)
        {
            if (ClassScope is not null || CourseUnitScope is not null || AcademicScope is not null || SupportScope is not null || OnboardingScope is not null) throw new InvalidOperationException("CalendarWriteDenied");
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!CalendarScope.Allows(change)) throw new InvalidOperationException("CalendarWriteDenied");
            return;
        }
        if (ChangeTracker.Entries<SchoolEvent>().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("CalendarWriteDenied");
        // A exclusividade antecede todos os retornos dos escopos existentes.
        if (ClassScope is not null)
        {
            if (CourseUnitScope is not null || AcademicScope is not null || SupportScope is not null || OnboardingScope is not null) throw new InvalidOperationException("ClassWriteDenied");
            foreach (var change in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                if (!ClassScope.Allows(change)) throw new InvalidOperationException("ClassWriteDenied");
            return;
        }
        if (ChangeTracker.Entries().Any(x => x.Entity is AcademicClass or AcademicClassUnit && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("ClassWriteDenied");
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
