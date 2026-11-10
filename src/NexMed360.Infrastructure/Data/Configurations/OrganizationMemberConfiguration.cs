using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        // Keep the existing table name so the code rename preserves stored data.
        EntityConfiguration.Organization(builder, "OrganizationMemberships");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.OrganizationId, row.UserId }).IsUnique();
        builder.HasOne<Location>().WithMany().HasForeignKey(row => new { row.LocationId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
