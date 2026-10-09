using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Identity;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Identity;

namespace SchoolGether.Infrastructure.Persistence;

public sealed class SchoolGetherDbContext(DbContextOptions<SchoolGetherDbContext> options, ICurrentAccount? current = null)
    : IdentityUserContext<ApplicationUser, int>(options)
{
    private int? InstitutionId => current?.InstitutionId;
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<InstitutionMember> Members => Set<InstitutionMember>();
    public DbSet<RefreshSession> Sessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolGetherDbContext).Assembly);
        IdentityModel.Configure(modelBuilder);
        modelBuilder.Entity<InstitutionMember>().HasQueryFilter(member =>
            member.DeletedAt == null && InstitutionId != null && member.InstitutionId == InstitutionId);
    }

    private void ValidateTenantWrites()
    {
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

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
}
