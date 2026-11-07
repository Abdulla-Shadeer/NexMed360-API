using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasOne(row => row.Person).WithOne().HasForeignKey<ApplicationUser>(row => row.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(row => row.Person).AutoInclude();
        builder.HasIndex(row => row.PersonId).IsUnique();
    }
}
