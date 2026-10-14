using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SchoolGether.Infrastructure.Persistence;

internal static class PropertyMappingConventions
{
    internal static void ApplyPropertyConventions<TEntity>(this EntityTypeBuilder<TEntity> builder) where TEntity : class
    {
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetColumnName(string.Concat(property.Name.Select((c, index) =>
                char.IsUpper(c) && index > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())));
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?)) property.SetColumnType("datetime(6)");
        }
    }
}
