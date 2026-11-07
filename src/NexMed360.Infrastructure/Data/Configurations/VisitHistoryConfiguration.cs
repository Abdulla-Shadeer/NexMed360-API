using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class VisitHistoryConfiguration : IEntityTypeConfiguration<VisitHistory>
{
    public void Configure(EntityTypeBuilder<VisitHistory> builder)
    {
        EntityConfiguration.Organization(builder, "VisitHistories");
        builder.HasOne<Patient>().WithMany(row => row.VisitHistories).HasForeignKey(row => row.PatientId).OnDelete(DeleteBehavior.Cascade);
    }
}
