using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class ReceptionistConfiguration : IEntityTypeConfiguration<Receptionist>
{
    public void Configure(EntityTypeBuilder<Receptionist> builder)
    {
        EntityConfiguration.Configure(builder, "Receptionists");
        builder.HasOne<Hospital>().WithMany().HasForeignKey(row => row.HospitalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithOne().HasForeignKey<Receptionist>(receptionist => receptionist.UserId);
    }
}
