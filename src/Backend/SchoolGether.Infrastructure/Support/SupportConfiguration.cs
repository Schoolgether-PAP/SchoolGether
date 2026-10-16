using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SchoolGether.Domain.Support;

namespace SchoolGether.Infrastructure.Support;

internal sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> b)
    {
        b.ToTable("support_ticket"); b.HasKey(x => x.Id);
        b.Property(x => x.Subject).HasMaxLength(150); b.Property(x => x.AuthorName).HasMaxLength(150);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CloseSummary).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.InstitutionId, x.AuthorId, x.ClientId }).IsUnique();
        b.HasIndex(x => new { x.InstitutionId, x.Status, x.Id });
        b.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Messages).HasField("_messages").UsePropertyAccessMode(PropertyAccessMode.Field);
        ConfigureColumns(b.Metadata);
    }
    internal static void ConfigureColumns(IMutableEntityType entity)
    {
        foreach (var property in entity.GetProperties())
        {
            property.SetColumnName(string.Concat(property.Name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString())));
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                property.SetValueConverter(new ValueConverter<DateTime, DateTime>(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc)));
        }
    }
}

internal sealed class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> b)
    {
        b.ToTable("support_message"); b.HasKey(x => x.Id);
        b.Property(x => x.AuthorName).HasMaxLength(150); b.Property(x => x.Text).HasMaxLength(4000);
        b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.TicketId, x.ClientId }).IsUnique();
        b.HasIndex(x => new { x.InstitutionId, x.TicketId, x.Id });
        b.Ignore(x => x.Attachments);
        SupportTicketConfiguration.ConfigureColumns(b.Metadata);
    }
}
