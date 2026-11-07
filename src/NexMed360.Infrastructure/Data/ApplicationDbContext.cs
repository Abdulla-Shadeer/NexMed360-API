using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Entities;

namespace NexMed360Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public const string NonCancelledSlotIndexName = "UX_Appointments_NonCancelledSlot";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public DbSet<Person> People => Set<Person>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();
    public DbSet<PractitionerRole> PractitionerRoles => Set<PractitionerRole>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OrganizationPatient> OrganizationPatients => Set<OrganizationPatient>();
    public DbSet<AccessRole> AccessRoles => Set<AccessRole>();
    public DbSet<MembershipAccessRole> MembershipAccessRoles => Set<MembershipAccessRole>();
    public DbSet<VisitHistory> VisitHistorys => Set<VisitHistory>();
    public DbSet<ClinicalRecord> ClinicalRecords => Set<ClinicalRecord>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<CancellationRequest> CancellationRequests => Set<CancellationRequest>();
    public DbSet<PractitionerSchedule> PractitionerSchedules => Set<PractitionerSchedule>();
    public DbSet<SessionSlot> SessionSlots => Set<SessionSlot>();
    public DbSet<SessionHistory> SessionHistories => Set<SessionHistory>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<EncounterParticipant> EncounterParticipants => Set<EncounterParticipant>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CheckOrganizationIds();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        CheckOrganizationIds();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void CheckOrganizationIds()
    {
        foreach (var entry in ChangeTracker.Entries<IOrganizationOwned>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified && entry.Entity.OrganizationId == Guid.Empty)
                throw new InvalidOperationException("Choose an organization before saving this record.");
            if (entry.State == EntityState.Modified && entry.Property(nameof(IOrganizationOwned.OrganizationId)).IsModified
                && !Equals(entry.Property(nameof(IOrganizationOwned.OrganizationId)).OriginalValue, entry.Entity.OrganizationId))
                throw new InvalidOperationException("A saved record cannot be moved to another organization.");
        }
    }
}
