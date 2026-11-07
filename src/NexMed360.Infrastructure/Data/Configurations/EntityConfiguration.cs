using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal static class EntityConfiguration
{
    internal static void Configure<T>(EntityTypeBuilder<T> builder, string table) where T : Entity
    {
        builder.ToTable(table);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.CreatedDate).ValueGeneratedOnAdd();
        builder.Property(entity => entity.UpdatedDate).ValueGeneratedOnAddOrUpdate();
    }
}
