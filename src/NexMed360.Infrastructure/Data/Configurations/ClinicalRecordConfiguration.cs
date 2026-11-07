using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class ClinicalRecordConfiguration : IEntityTypeConfiguration<ClinicalRecord>
{
    public void Configure(EntityTypeBuilder<ClinicalRecord> builder)
    {
        EntityConfiguration.Organization(builder, "ClinicalRecords");
        builder.HasOne<Patient>().WithMany().HasForeignKey(row => row.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PractitionerRole>().WithMany().HasForeignKey(row => new { row.PractitionerRoleId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Encounter>().WithMany().HasForeignKey(row => new { row.EncounterId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
