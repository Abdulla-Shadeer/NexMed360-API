using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal static class EntityConfiguration
{
    internal static void Configure<T>(EntityTypeBuilder<T> builder, string table) where T : Entity
    {
        builder.ToTable(table);
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedNever();
    }

    internal static void Organization<T>(EntityTypeBuilder<T> builder, string table) where T : Entity, IOrganizationOwned
    {
        Configure(builder, table);
        builder.HasAlternateKey(row => new { row.Id, row.OrganizationId });
        builder.HasOne<Organization>().WithMany().HasForeignKey(row => row.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
