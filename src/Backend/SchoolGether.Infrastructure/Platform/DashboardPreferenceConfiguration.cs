using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolGether.Infrastructure.Identity;

namespace SchoolGether.Infrastructure.Platform;

internal sealed class DashboardPreferenceConfiguration : IEntityTypeConfiguration<DashboardPreferenceEntry>
{
    public void Configure(EntityTypeBuilder<DashboardPreferenceEntry> builder)
    {
        builder.ToTable("dashboard_preference");
        builder.HasKey(x => x.AccountId);
        builder.Property(x => x.AccountId).HasColumnName("account_id").ValueGeneratedNever();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.WidgetsJson).HasColumnName("widgets_json").HasMaxLength(1024);
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<DashboardPreferenceEntry>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
