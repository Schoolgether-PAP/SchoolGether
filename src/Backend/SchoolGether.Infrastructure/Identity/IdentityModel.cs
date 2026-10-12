using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Domain.Identity;
using SchoolGether.Domain.Institutions;

namespace SchoolGether.Infrastructure.Identity;

internal static class IdentityModel
{
    internal static void Configure(ModelBuilder model)
    {
        var user = model.Entity<ApplicationUser>();
        user.ToTable("user_account");
        user.Property(x => x.Name).HasMaxLength(150);
        user.HasIndex(x => x.NormalizedEmail).IsUnique();
        user.Property(x => x.RowVersion).IsConcurrencyToken();
        user.Ignore(x => x.IsMaster);
        user.Property(x => x.IsActive).HasDefaultValue(true);
        user.Property(x => x.PermissionsJson).HasMaxLength(512).HasDefaultValue("[]");
        // NULL para todas as restantes contas; o índice garante um só master mesmo com concorrência.
        user.HasIndex(x => x.MasterSlot).IsUnique();
        var member = model.Entity<InstitutionMember>();
        member.ToTable("institution_member");
        member.Property(x => x.Name).HasMaxLength(150);
        member.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        member.Property(x => x.RowVersion).IsConcurrencyToken();
        // Uma conta tem, no máximo, uma instituição.
        member.HasIndex(x => x.UserId).IsUnique();
        member.HasOne<ApplicationUser>().WithOne().HasForeignKey<InstitutionMember>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        member.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<IdentityUserClaim<int>>().ToTable("user_claim");
        model.Entity<IdentityUserLogin<int>>().ToTable("user_login");
        model.Entity<IdentityUserToken<int>>().ToTable("user_token");
        var session = model.Entity<RefreshSession>();
        session.ToTable("refresh_session");
        session.Property(x => x.TokenHash).HasMaxLength(64);
        session.HasIndex(x => x.TokenHash).IsUnique();
        session.HasIndex(x => x.FamilyId);
        session.Property(x => x.SecurityStamp).HasMaxLength(256);
        session.Property(x => x.RowVersion).IsConcurrencyToken();
        session.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        foreach (var entity in model.Model.GetEntityTypes().Where(x => x.ClrType.Namespace?.Contains("Identity") == true))
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase(property.Name));
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetColumnType("datetime(6)");
            }
    }

    private static string SnakeCase(string name) => string.Concat(name.Select((c, index) =>
        char.IsUpper(c) && index > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
