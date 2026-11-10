using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class MembershipAccessRoleConfiguration : IEntityTypeConfiguration<MembershipAccessRole>
{
    public void Configure(EntityTypeBuilder<MembershipAccessRole> builder)
    {
        EntityConfiguration.Organization(builder, "MembershipAccessRoles");
        builder.HasOne<OrganizationMembership>().WithMany(member => member.RoleAssignments).HasForeignKey(row => new { row.MembershipId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AccessRole>().WithMany().HasForeignKey(row => new { row.AccessRoleId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.MembershipId, row.AccessRoleId }).IsUnique();
    }
}
