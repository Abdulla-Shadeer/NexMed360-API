using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class PractitionerScheduleConfiguration : IEntityTypeConfiguration<PractitionerSchedule>
{
    public void Configure(EntityTypeBuilder<PractitionerSchedule> builder)
    {
        EntityConfiguration.Organization(builder, "PractitionerSchedules");
        builder.HasOne<PractitionerRole>().WithMany().HasForeignKey(row => new { row.PractitionerRoleId, row.OrganizationId })
            .HasPrincipalKey(row => new { row.Id, row.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(row => row.Occurrences).HasConversion(
            value => System.Text.Json.JsonSerializer.Serialize(value, (System.Text.Json.JsonSerializerOptions)null!),
            value => System.Text.Json.JsonSerializer.Deserialize<List<NexMed360.API.Entities.Enums.SessionOccurrence>>(value, (System.Text.Json.JsonSerializerOptions)null!) ?? new(),
            new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<NexMed360.API.Entities.Enums.SessionOccurrence>>(
                (first, second) => first != null && second != null && first.SequenceEqual(second),
                value => value.Aggregate(0, (hash, occurrence) => HashCode.Combine(hash, occurrence.GetHashCode())),
                value => value.ToList()));
        builder.Ignore(row => row.TotalSlots);
    }
}
