using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        EntityConfiguration.Configure(builder, "Doctors");
        builder.HasOne<Hospital>().WithMany().HasForeignKey(row => row.HospitalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithOne().HasForeignKey<Doctor>(doctor => doctor.UserId);
    }
}
