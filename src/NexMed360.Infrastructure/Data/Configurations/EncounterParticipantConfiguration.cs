using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class EncounterParticipantConfiguration : IEntityTypeConfiguration<EncounterParticipant>
{
    public void Configure(EntityTypeBuilder<EncounterParticipant> builder)
    {
        EntityConfiguration.Organization(builder, "EncounterParticipants");
        builder.HasOne<Encounter>().WithMany(row => row.Participants).HasForeignKey(row => new { row.EncounterId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PractitionerRole>().WithMany().HasForeignKey(row => new { row.PractitionerRoleId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.EncounterId, row.PractitionerRoleId }).IsUnique();
    }
}
