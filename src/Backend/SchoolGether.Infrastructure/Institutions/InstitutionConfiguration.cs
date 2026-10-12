using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolGether.Domain.Institutions;

namespace SchoolGether.Infrastructure.Institutions;

internal sealed class InstitutionConfiguration : IEntityTypeConfiguration<Institution>
{
    public void Configure(EntityTypeBuilder<Institution> builder)
    {
        builder.ToTable("institution");
        builder.HasKey(institution => institution.Id);
        builder.Property(institution => institution.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(institution => institution.Name)
            .HasColumnName("name").HasMaxLength(Institution.MaxNameLength).IsRequired();
        builder.Property(institution => institution.CreatedAt)
            .HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(institution => institution.UpdatedAt)
            .HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(institution => institution.CreatedBy).HasColumnName("created_by");
        builder.Property(institution => institution.DeletedAt)
            .HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(institution => institution.RowVersion)
            .HasColumnName("row_version").IsConcurrencyToken();

        builder.HasQueryFilter(institution => institution.DeletedAt == null);
    }
}
