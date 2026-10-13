using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolGether.Domain.Audit;

namespace SchoolGether.Infrastructure.Audit;

internal sealed class AuditConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> entry)
    {
        entry.ToTable("audit_entry");
        entry.HasKey(x => x.Id);
        entry.Property(x => x.CreatedAt).HasColumnType("datetime(6)").HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        entry.Property(x => x.ActorName).HasMaxLength(150);
        entry.Property(x => x.Action).HasMaxLength(100);
        entry.Property(x => x.Target).HasMaxLength(150);
        entry.Property(x => x.Result).HasMaxLength(32);
        entry.Property(x => x.Reason).HasMaxLength(500);
        entry.Property(x => x.Before).HasColumnType("text");
        entry.Property(x => x.After).HasColumnType("text");
        entry.Property(x => x.RequestReference).HasMaxLength(100);
        entry.HasIndex(x => x.CreatedAt);
        foreach (var property in entry.Metadata.GetProperties())
            property.SetColumnName(string.Concat(property.Name.Select((c, i) =>
                char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())));
    }
}
