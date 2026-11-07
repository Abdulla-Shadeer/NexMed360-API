using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class AccessRoleConfiguration : IEntityTypeConfiguration<AccessRole>
{
    public void Configure(EntityTypeBuilder<AccessRole> builder)
    {
        EntityConfiguration.Organization(builder, "AccessRoles");
        builder.Property(row => row.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(row => new { row.OrganizationId, row.Name }).IsUnique();
    }
}
