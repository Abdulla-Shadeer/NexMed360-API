using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexMed360.API.Entities.Enums;
using System.Text.Json;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class DoctorSessionConfiguration : IEntityTypeConfiguration<DoctorSession>
{
    public void Configure(EntityTypeBuilder<DoctorSession> builder)
    {
        EntityConfiguration.Configure(builder, "DoctorSessions");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(row => row.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Hospital>().WithMany().HasForeignKey(row => row.HospitalId).OnDelete(DeleteBehavior.Cascade);
        var converter = new ValueConverter<List<SessionOccurrence>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null!),
            value => JsonSerializer.Deserialize<List<SessionOccurrence>>(value, (JsonSerializerOptions)null!) ?? new());
        var comparer = new ValueComparer<List<SessionOccurrence>>(
            (first, second) => first != null && second != null && first.SequenceEqual(second),
            value => value.Aggregate(0, (hash, occurrence) => HashCode.Combine(hash, occurrence.GetHashCode())),
            value => value.ToList());
        builder.Property(session => session.Occurrences).HasConversion(converter, comparer);
    }
}
