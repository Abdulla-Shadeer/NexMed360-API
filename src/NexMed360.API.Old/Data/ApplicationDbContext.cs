using NexMed360Api.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using NexMed360.API.Entities.Enums;
using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User>(options)
{
    public const string NonCancelledSlotIndexName = "UX_Appointments_NonCancelledSlot";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Keep the existing FK index and add a separate filtered uniqueness rule.
        builder.Entity<Appointment>().HasIndex(appointment => appointment.SessionSlotId);
        builder.Entity<Appointment>()
            .HasIndex(appointment => appointment.SessionSlotId, NonCancelledSlotIndexName)
            .IsUnique()
            .HasFilter($"\"Status\" <> {(int)AppointmentStatus.Cancelled}");

        builder
            .Entity<VisitHistory>()
            .Property(vh => vh.VisitDate)
            .ValueGeneratedOnAdd()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);

        var occurrenceConverter = new ValueConverter<List<SessionOccurrence>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
            v => JsonSerializer.Deserialize<List<SessionOccurrence>>(v, (JsonSerializerOptions)null)
            ?? new List<SessionOccurrence>());

        var occurrenceComparer = new ValueComparer<List<SessionOccurrence>>(
            (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        builder.Entity<DoctorSession>()
            .Property(ds => ds.Occurrences)
            .HasConversion(occurrenceConverter, occurrenceComparer);
    }

    public DbSet<Patient> Patients { get; set; }
    public DbSet<Doctor> Doctors { get; set; }
    public DbSet<Hospital> Hospitals { get; set; }
    public DbSet<HospitalAdmin> HospitalAdmins { get; set; }
    public DbSet<Receptionist> Receptionists { get; set; }
    public DbSet<VisitHistory> VisitHistorys { get; set; }
    public DbSet<ClinicRecord> ClinicRecords { get; set; }
    public DbSet<Prescription> Prescriptions { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<CancellationRequest> CancellationRequests { get; set; }
    public DbSet<DoctorSession> DoctorSessions { get; set; }
    public DbSet<SessionSlot> SessionSlots { get; set; }
    public DbSet<SessionHistory> SessionHistories { get; set; }
}
