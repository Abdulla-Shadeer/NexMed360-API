using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        EntityConfiguration.Organization(builder, "Prescriptions");
        builder.Ignore(row => row.IsActive);
        builder.Property(row => row.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PrescriptionStatus.Active);
        builder.Property(row => row.Intent).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PrescriptionIntent.Order);
        builder.ToTable("Prescriptions", table =>
        {
            table.HasCheckConstraint("CK_Prescriptions_Status", "\"Status\" IN ('Active','OnHold','Cancelled','Completed','EnteredInError','Stopped','Draft','Unknown')");
            table.HasCheckConstraint("CK_Prescriptions_Intent", "\"Intent\" IN ('Proposal','Plan','Order','OriginalOrder','ReflexOrder','FillerOrder','InstanceOrder','Option')");
        });
        builder.HasOne(row => row.ClinicalRecord).WithMany(row => row.Prescriptions).HasForeignKey(row => new { row.ClinicalRecordId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Cascade);
    }
}
