using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SchoolGether.Domain.Audit;
using SchoolGether.Domain.Billing;
using SchoolGether.Domain.Identity;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Onboarding;

// Capacidade interna, ligada a referências novas e à transação que as criou. Nunca altera o principal.
internal sealed class OnboardingWriteScope(SchoolGetherDbContext db)
{
    private readonly Guid transactionId = db.Database.CurrentTransaction?.TransactionId
        ?? throw new InvalidOperationException("OnboardingTransactionRequired");
    private readonly HashSet<object> inserted = new(ReferenceEqualityComparer.Instance);
    private int? ownerId;
    private int? institutionId;
    private ApplicationUser? owner;
    private Institution? institution;
    private InstitutionMember? member;
    private InstitutionProfile? profile;
    private DemonstrationSubscription? subscription;

    internal void BindOwner(ApplicationUser value)
    {
        if (owner is not null || value.Id != 0 || db.Entry(value).State != EntityState.Detached)
            throw new InvalidOperationException("OnboardingWriteDenied");
        owner = value;
    }
    internal void BindInstitution(Institution value)
    {
        RequireOwner(value.CreatedBy ?? 0);
        if (institution is not null || value.Id != 0 || db.Entry(value).State != EntityState.Detached)
            throw new InvalidOperationException("OnboardingWriteDenied");
        institution = value;
    }
    internal ApplicationUser RequireOwner(int ownerId)
    {
        if (owner is null || this.ownerId is null || owner.Id != this.ownerId || owner.Id != ownerId || db.Entry(owner).State != EntityState.Unchanged)
            throw new InvalidOperationException("OnboardingWriteDenied");
        return owner;
    }
    internal void RequireInstitution(int institutionId, int ownerId)
    {
        RequireOwner(ownerId);
        if (institution is null || this.institutionId is null || institution.Id != this.institutionId || institution.Id != institutionId ||
            db.Entry(institution).State != EntityState.Unchanged)
            throw new InvalidOperationException("OnboardingWriteDenied");
    }
    internal void BindMember(InstitutionMember value)
    {
        RequireInstitution(value.InstitutionId, value.UserId);
        if (member is not null || value.CreatedBy != owner!.Id || value.Role != AccountRole.InstitutionAdmin)
            throw new InvalidOperationException("OnboardingWriteDenied");
        member = value;
    }
    internal void BindProfile(InstitutionProfile value)
    {
        RequireInstitution(value.InstitutionId, value.CreatedBy);
        if (profile is not null) throw new InvalidOperationException("OnboardingWriteDenied");
        profile = value;
    }
    internal void BindSubscription(DemonstrationSubscription value)
    {
        RequireInstitution(value.InstitutionId, value.CreatedBy);
        if (subscription is not null) throw new InvalidOperationException("OnboardingWriteDenied");
        subscription = value;
    }
    // Depois de cada SaveChanges bem sucedido, a autorização de inserção fica consumida.
    internal void RecordSavedEntities()
    {
        Remember(owner, owner?.Id ?? 0);
        Remember(institution, institution?.Id ?? 0);
        Remember(member, member?.Id ?? 0);
        Remember(profile, profile?.Id ?? 0);
        Remember(subscription, subscription?.Id ?? 0);
        if (owner?.Id > 0) ownerId ??= owner.Id;
        if (institution?.Id > 0) institutionId ??= institution.Id;
    }
    private void Remember(object? entity, int id)
    {
        if (entity is not null && id > 0) inserted.Add(entity);
    }
    internal bool Allows(EntityEntry entry)
    {
        if (db.Database.CurrentTransaction?.TransactionId != transactionId) return false;
        if (ReferenceEquals(entry.Entity, owner))
            return !owner!.IsPlatformAdmin && !owner.IsMaster && owner.DeletedAt is null &&
                ((entry.State == EntityState.Added && !inserted.Contains(owner) && owner.Id == 0 && owner.CreatedBy is null) ||
                 (entry.State == EntityState.Modified && owner.Id == ownerId && owner.CreatedBy == ownerId &&
                  entry.Property(nameof(ApplicationUser.CreatedBy)).OriginalValue is null &&
                  entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name == nameof(ApplicationUser.CreatedBy))));
        if (entry.State != EntityState.Added || inserted.Contains(entry.Entity)) return false;
        return (ReferenceEquals(entry.Entity, institution) && institution!.Id == 0 && institution.CreatedBy == ownerId) ||
            (ReferenceEquals(entry.Entity, member) && member!.Id == 0 && member.UserId == ownerId &&
                member.InstitutionId == institutionId && member.CreatedBy == ownerId && member.Role == AccountRole.InstitutionAdmin) ||
            (ReferenceEquals(entry.Entity, profile) && profile!.Id == 0 && profile.InstitutionId == institutionId && profile.CreatedBy == ownerId) ||
            (ReferenceEquals(entry.Entity, subscription) && subscription!.Id == 0 && subscription.InstitutionId == institutionId && subscription.CreatedBy == ownerId) ||
            (entry.Entity is AuditEntry audit && ownerId.HasValue && institutionId.HasValue &&
             audit.ActorId == ownerId && audit.InstitutionId == institutionId && audit.Action == "onboarding.institution.create");
    }
}
