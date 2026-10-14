using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolGether.Infrastructure.Persistence;
using SchoolGether.Domain.Billing;
using SchoolGether.Domain.Institutions;

namespace SchoolGether.Infrastructure.Billing;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<DemonstrationSubscription>
{
    public void Configure(EntityTypeBuilder<DemonstrationSubscription> builder)
    {
        builder.ToTable("demonstration_subscription");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.InstitutionId).IsUnique();
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.PlanId).HasMaxLength(32);
        builder.Property(x => x.PlanNameKey).HasMaxLength(100);
        builder.Property(x => x.CatalogVersion).HasMaxLength(64);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.MonthlyPrice).HasPrecision(10, 2);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasOne<Institution>().WithOne().HasForeignKey<DemonstrationSubscription>(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        builder.ApplyPropertyConventions();
    }
}
