using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolGether.Domain.Institutions;

namespace SchoolGether.Infrastructure.Institutions;

internal sealed class InstitutionProfileConfiguration : IEntityTypeConfiguration<InstitutionProfile>
{
    public void Configure(EntityTypeBuilder<InstitutionProfile> builder)
    {
        builder.ToTable("institution_profile");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.InstitutionId).IsUnique();
        builder.Property(x => x.InstitutionType).HasMaxLength(32);
        builder.Property(x => x.Locality).HasMaxLength(100);
        builder.Property(x => x.AcademicYear).HasMaxLength(20);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasOne<Institution>().WithOne().HasForeignKey<InstitutionProfile>(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetColumnName(string.Concat(property.Name.Select((c, index) =>
                char.IsUpper(c) && index > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())));
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?)) property.SetColumnType("datetime(6)");
        }
    }
}
