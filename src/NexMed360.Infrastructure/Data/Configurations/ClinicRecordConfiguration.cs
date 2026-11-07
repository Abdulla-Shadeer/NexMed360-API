using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class ClinicRecordConfiguration : IEntityTypeConfiguration<ClinicRecord>
{
    public void Configure(EntityTypeBuilder<ClinicRecord> builder)
    {
        EntityConfiguration.Configure(builder, "ClinicRecords");
        builder.HasOne<Patient>().WithMany().HasForeignKey(row => row.PatientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(row => row.DoctorId).OnDelete(DeleteBehavior.Cascade);

    }
}
