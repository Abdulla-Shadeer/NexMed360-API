using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class PractitionerConfiguration : IEntityTypeConfiguration<Practitioner>
{
    public void Configure(EntityTypeBuilder<Practitioner> builder)
    {
        EntityConfiguration.Configure(builder, "Practitioners");
        builder.HasOne<Person>().WithOne().HasForeignKey<Practitioner>(row => row.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.PersonId).IsUnique();
    }
}
