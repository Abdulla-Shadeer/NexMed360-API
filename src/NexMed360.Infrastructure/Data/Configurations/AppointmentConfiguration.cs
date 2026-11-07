using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        EntityConfiguration.Organization(builder, "Appointments");
        builder.HasOne<Patient>().WithMany().HasForeignKey(row => row.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PractitionerRole>().WithMany().HasForeignKey(row => new { row.PractitionerRoleId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionSlot>().WithMany().HasForeignKey(row => new { row.SessionSlotId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(row => row.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.SessionSlotId, NexMed360Api.Data.ApplicationDbContext.NonCancelledSlotIndexName)
            .IsUnique().HasFilter($"\"Status\" <> {(int)NexMed360Api.Entities.Enums.AppointmentStatus.Cancelled}");
    }
}
