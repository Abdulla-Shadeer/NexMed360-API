using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        EntityConfiguration.Configure(builder, "Patients");
        builder.HasOne<User>().WithOne().HasForeignKey<Patient>(patient => patient.UserId);
    }
}
