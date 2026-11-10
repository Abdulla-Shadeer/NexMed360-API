using NexMed360.Domain.Rules;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Entities;
using NexMed360.Application.Access;
using NexMed360Api.Common.Auth;

namespace NexMed360Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, WorkspaceAccess? workspace = null) : IdentityDbContext<ApplicationUser>(options)
{
    public WorkspaceAccess Access { get; } = workspace ?? new();

    public const string NonCancelledSlotIndexName = "UX_Appointments_NonCancelledSlot";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ApplyWorkspaceFilters(builder);
    }

    private void Scope<T>(ModelBuilder builder) where T : Entity, IOrganizationOwned =>
        builder.Entity<T>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Platform || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId);

    private void ApplyWorkspaceFilters(ModelBuilder builder)
    {
        Scope<AccessRole>(builder);
        Scope<MembershipAccessRole>(builder);

        builder.Entity<OrganizationPatient>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId);

        builder.Entity<VisitHistory>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId);

        builder.Entity<CancellationRequest>().HasQueryFilter(row => !Access.IsSelected
            || Appointments.Any(appointment => appointment.Id == row.AppointmentId));

        builder.Entity<EncounterParticipant>().HasQueryFilter(row => !Access.IsSelected
            || Encounters.Any(encounter => encounter.Id == row.EncounterId));

        builder.Entity<OrganizationMembership>().HasQueryFilter(row => !Access.IsSelected || Access.Mode == WorkspaceModes.Platform
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId
                && (!Access.HasLocationScope || row.LocationId.HasValue && Access.LocationIds.Contains(row.LocationId.Value)));

        builder.Entity<Location>().HasQueryFilter(row => !Access.IsSelected || Access.Mode == WorkspaceModes.Platform
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId
                && (!Access.HasLocationScope || Access.LocationIds.Contains(row.Id)));

        builder.Entity<PractitionerRole>().HasQueryFilter(row => !Access.IsSelected || Access.Mode == WorkspaceModes.Patient
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId
                && (!Access.HasLocationScope || row.LocationId.HasValue && Access.LocationIds.Contains(row.LocationId.Value)));

        builder.Entity<Patient>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Patient && row.Id == Access.PatientId
            || Access.Mode == WorkspaceModes.Work && (OrganizationPatients.Any(link => link.PatientId == row.Id)
                || Appointments.Any(appointment => appointment.PatientId == row.Id)
                || ClinicalRecords.Any(record => record.PatientId == row.Id)));

        builder.Entity<Appointment>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Patient && row.PatientId == Access.PatientId
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId
                && (Access.Has(Permissions.AppointmentsManage) || Access.Has(Permissions.EncountersStart))
                && PractitionerRoles.Any(role => role.Id == row.PractitionerRoleId));

        builder.Entity<ClinicalRecord>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Patient && row.PatientId == Access.PatientId && row.IsPatientVisible
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId
                && Access.Has(Permissions.ClinicalRecordsRead) && PractitionerRoles.Any(role => role.Id == row.PractitionerRoleId));

        builder.Entity<Prescription>().HasQueryFilter(row => !Access.IsSelected
            || ClinicalRecords.Any(record => record.Id == row.ClinicalRecordId));

        builder.Entity<PractitionerSchedule>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Patient && row.IsActive
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId
                && PractitionerRoles.Any(role => role.Id == row.PractitionerRoleId));

        builder.Entity<SessionSlot>().HasQueryFilter(row => !Access.IsSelected
            || PractitionerSchedules.Any(schedule => schedule.Id == row.SessionId));

        builder.Entity<SessionHistory>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId
                && PractitionerSchedules.Any(schedule => schedule.Id == row.PractitionerScheduleId));

        builder.Entity<Encounter>().HasQueryFilter(row => !Access.IsSelected
            || Access.Mode == WorkspaceModes.Work && row.OrganizationId == Access.OrganizationId
                && (!Access.HasLocationScope || row.LocationId.HasValue && Access.LocationIds.Contains(row.LocationId.Value)));
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
            if (Access.IsSelected && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                if (Access.Mode == WorkspaceModes.Work && entry.Entity.OrganizationId != Access.OrganizationId)
                    throw new UnauthorizedAccessException("This record belongs to another organization.");

                if (Access.Mode == WorkspaceModes.Platform && entry.Entity is not OrganizationMembership and not AccessRole and not MembershipAccessRole and not Location
                    && !(entry.State == EntityState.Deleted && entry.Entity is PractitionerRole or PractitionerSchedule or SessionSlot))
                    throw new UnauthorizedAccessException("Platform administration does not allow clinical changes.");

                if (Access.Mode == WorkspaceModes.Patient && entry.Entity is not Appointment and not CancellationRequest and not SessionSlot)
                    throw new UnauthorizedAccessException("Patient mode does not allow this change.");
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                Guid? originalOrganizationId = entry.State == EntityState.Modified
                    ? (Guid)entry.Property(nameof(IOrganizationOwned.OrganizationId)).OriginalValue!
                    : null;

                OrganizationRules.ValidateOwnership(entry.Entity.OrganizationId, originalOrganizationId);
            }
        }
    }
}
