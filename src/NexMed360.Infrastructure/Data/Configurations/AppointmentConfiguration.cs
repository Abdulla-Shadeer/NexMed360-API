using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;
using NexMed360Api.Data;
using NexMed360Api.Entities.Enums;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        EntityConfiguration.Configure(builder, "Appointments");
        builder.HasOne<Patient>().WithMany().HasForeignKey(row => row.PatientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(row => row.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Hospital>().WithMany().HasForeignKey(row => row.HospitalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SessionSlot>().WithMany().HasForeignKey(row => row.SessionSlotId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(appointment => appointment.CreatedByUserId);
        builder.HasIndex(appointment => appointment.SessionSlotId);
        builder.HasIndex(appointment => appointment.SessionSlotId, ApplicationDbContext.NonCancelledSlotIndexName)
            .IsUnique().HasFilter($"\"Status\" <> {(int)AppointmentStatus.Cancelled}");
    }
}
