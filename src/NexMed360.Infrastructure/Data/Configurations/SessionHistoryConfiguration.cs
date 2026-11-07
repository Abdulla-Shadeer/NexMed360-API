using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class SessionHistoryConfiguration : IEntityTypeConfiguration<SessionHistory>
{
    public void Configure(EntityTypeBuilder<SessionHistory> builder)
    {
        EntityConfiguration.Organization(builder, "SessionHistories");
        builder.HasOne(row => row.PractitionerSchedule).WithMany(row => row.History).HasForeignKey(row => new { row.PractitionerScheduleId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Cascade);
    }
}
