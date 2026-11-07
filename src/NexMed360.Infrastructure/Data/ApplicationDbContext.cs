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

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
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
