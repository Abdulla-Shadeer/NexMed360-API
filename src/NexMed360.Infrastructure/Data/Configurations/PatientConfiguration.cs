using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        EntityConfiguration.Configure(builder, "Patients");
        builder.HasOne<Person>().WithOne().HasForeignKey<Patient>(row => row.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.PersonId).IsUnique();
        builder.HasMany(row => row.VisitHistories).WithOne().HasForeignKey(row => row.PatientId).OnDelete(DeleteBehavior.Cascade);
    }
}
