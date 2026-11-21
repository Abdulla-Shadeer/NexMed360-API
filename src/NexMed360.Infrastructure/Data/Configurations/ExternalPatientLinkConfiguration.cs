using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class ExternalPatientLinkConfiguration : IEntityTypeConfiguration<ExternalPatientLink>
{
    public void Configure(EntityTypeBuilder<ExternalPatientLink> builder)
    {
        EntityConfiguration.Configure(builder, "ExternalPatientLinks");
        builder.Property(row => row.PartnerId).HasMaxLength(64).IsRequired();
        builder.Property(row => row.ExternalPatientId).HasMaxLength(64).IsRequired();
        builder.Property(row => row.ConfirmedByUserId).HasMaxLength(450).IsRequired();
        builder.HasOne<Patient>().WithMany().HasForeignKey(row => row.LocalPatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(row => row.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.PartnerId, row.ExternalPatientId }).IsUnique();
        builder.HasIndex(row => new { row.PartnerId, row.LocalPatientId }).IsUnique();
        builder.ToTable("ExternalPatientLinks", table => table.HasCheckConstraint("CK_ExternalPatientLinks_Identity",
            "length(btrim(\"PartnerId\")) > 0 AND length(btrim(\"ExternalPatientId\")) > 0 AND length(btrim(\"ConfirmedByUserId\")) > 0"));
    }
}
