using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class SessionSlotConfiguration : IEntityTypeConfiguration<SessionSlot>
{
    public void Configure(EntityTypeBuilder<SessionSlot> builder)
    {
        EntityConfiguration.Configure(builder, "SessionSlots");

    }
}
