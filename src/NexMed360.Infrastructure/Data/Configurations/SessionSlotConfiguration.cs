using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class SessionSlotConfiguration : IEntityTypeConfiguration<SessionSlot>
{
    public void Configure(EntityTypeBuilder<SessionSlot> builder)
    {
        EntityConfiguration.Organization(builder, "SessionSlots");
        builder.HasOne(row => row.Session).WithMany(row => row.SessionSlots).HasForeignKey(row => new { row.SessionId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(row => new { row.SessionId, row.Date, row.SlotNumber }).IsUnique();
    }
}
