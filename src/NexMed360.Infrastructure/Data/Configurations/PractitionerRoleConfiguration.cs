using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class PractitionerRoleConfiguration : IEntityTypeConfiguration<PractitionerRole>
{
    public void Configure(EntityTypeBuilder<PractitionerRole> builder)
    {
        EntityConfiguration.Organization(builder, "PractitionerRoles");
        builder.HasOne<Practitioner>().WithMany().HasForeignKey(row => row.PractitionerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(row => new { row.LocationId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
