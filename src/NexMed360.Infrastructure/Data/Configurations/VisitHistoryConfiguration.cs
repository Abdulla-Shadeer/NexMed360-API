using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class VisitHistoryConfiguration : IEntityTypeConfiguration<VisitHistory>
{
    public void Configure(EntityTypeBuilder<VisitHistory> builder)
    {
        EntityConfiguration.Configure(builder, "VisitHistorys");
        builder.HasOne<Hospital>().WithMany().HasForeignKey(row => row.HospitalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(history => history.UserId);
        builder.Property(history => history.VisitDate).ValueGeneratedOnAdd()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);
    }
}
