using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        EntityConfiguration.Configure(builder, "People");
        builder.Property(row => row.FirstName).HasMaxLength(250).IsRequired();
        builder.Property(row => row.LastName).HasMaxLength(250).IsRequired();
    }
}
