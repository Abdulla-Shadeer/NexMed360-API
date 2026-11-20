using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class RecordSharingRequestConfiguration : IEntityTypeConfiguration<RecordSharingRequest>
{
    public void Configure(EntityTypeBuilder<RecordSharingRequest> builder)
    {
        EntityConfiguration.Configure(builder, "RecordSharingRequests");
        builder.Property(row => row.Purpose).HasMaxLength(1000);
        // Keep existing column names; this change only makes the code clearer.
        builder.Property(row => row.RecordFromDate).HasColumnName("FromDate");
        builder.Property(row => row.RecordToDate).HasColumnName("ToDate");
        builder.Property(row => row.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
        builder.Property(row => row.DecisionByUserId).HasMaxLength(450);
        builder.HasOne<Patient>().WithMany().HasForeignKey(row => row.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(row => row.ReceivingOrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PractitionerRole>().WithMany().HasForeignKey(row => new { row.PractitionerRoleId, row.ReceivingOrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.PatientId, row.ReceivingOrganizationId, row.Status, row.ExpiresAt });
    }
}
