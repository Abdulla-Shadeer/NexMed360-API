using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class OrganizationPatientConfiguration : IEntityTypeConfiguration<OrganizationPatient>
{
    public void Configure(EntityTypeBuilder<OrganizationPatient> builder)
    {
        EntityConfiguration.Organization(builder, "OrganizationPatients");
        builder.HasOne<Patient>().WithMany().HasForeignKey(row => row.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.OrganizationId, row.PatientId }).IsUnique();
        builder.HasIndex(row => new { row.OrganizationId, row.PatientNumber }).IsUnique().HasFilter("\"PatientNumber\" IS NOT NULL");
    }
}
