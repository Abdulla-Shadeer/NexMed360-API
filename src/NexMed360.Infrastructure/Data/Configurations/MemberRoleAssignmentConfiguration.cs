using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class MemberRoleAssignmentConfiguration : IEntityTypeConfiguration<MemberRoleAssignment>
{
    public void Configure(EntityTypeBuilder<MemberRoleAssignment> builder)
    {
        // Keep existing storage names; only the code names change.
        EntityConfiguration.Organization(builder, "MembershipAccessRoles");
        builder.Property(row => row.OrganizationMemberId).HasColumnName("MembershipId");
        builder.HasOne<OrganizationMember>().WithMany(member => member.RoleAssignments).HasForeignKey(row => new { row.OrganizationMemberId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AccessRole>().WithMany().HasForeignKey(row => new { row.AccessRoleId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.OrganizationMemberId, row.AccessRoleId }).IsUnique();
    }
}
