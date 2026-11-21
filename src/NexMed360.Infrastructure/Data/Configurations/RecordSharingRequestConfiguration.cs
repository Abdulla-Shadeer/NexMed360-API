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
        builder.Property(row => row.ReceivingPartnerId).HasMaxLength(64);
        builder.Property(row => row.RequesterName).HasMaxLength(200);
        builder.Property(row => row.RequesterRole).HasMaxLength(150);
        builder.Property(row => row.RequesterContact).HasMaxLength(320);
        // Local recipients keep their foreign keys; external recipients use a configured partner and snapshots.
        builder.ToTable("RecordSharingRequests", table => table.HasCheckConstraint("CK_RecordSharingRequests_Recipient", """
            ("ReceivingPartnerId" IS NULL AND "ReceivingOrganizationId" IS NOT NULL AND "PractitionerRoleId" IS NOT NULL)
            OR ("ReceivingPartnerId" IS NOT NULL AND length(btrim("ReceivingPartnerId")) > 0
                AND "ReceivingOrganizationId" IS NULL AND "PractitionerRoleId" IS NULL
                AND "RequesterName" IS NOT NULL AND length(btrim("RequesterName")) > 0
                AND "RequesterRole" IS NOT NULL AND length(btrim("RequesterRole")) > 0
                AND "RequesterContact" IS NOT NULL AND length(btrim("RequesterContact")) > 0)
            """));
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
        builder.HasIndex(row => new { row.PatientId, row.ReceivingPartnerId, row.Status, row.ExpiresAt });
    }
}
