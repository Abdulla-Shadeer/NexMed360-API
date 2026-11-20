using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Common.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360.Infrastructure.Repositories;

public class ClinicalRecordRepository(ApplicationDbContext context, ILogger<ClinicalRecordRepository>? logger = null) : IClinicalRecordRepository
{
    public async Task<ClinicalRecord> Add(ClinicalRecord record)
    {
        await context.ClinicalRecords.AddAsync(record);
        await context.SaveChangesAsync();
        return record;
    }

    private IQueryable<ClinicalRecord> Readable(string callerId, bool isPractitionerRole)
    {
        if (isPractitionerRole && context.Access.Mode == WorkspaceModes.Work)
        {
            if (!context.Access.Has(Permissions.ClinicalRecordsRead))
                return context.ClinicalRecords.Where(record => false);

            var organizationId = context.Access.OrganizationId;
            var now = DateTime.UtcNow;
            var roleIds = context.PractitionerRoles.IgnoreQueryFilters()
                .Where(role => role.OrganizationId == organizationId
                    && (!context.Access.HasLocationScope || role.LocationId.HasValue && context.Access.LocationIds.Contains(role.LocationId.Value)))
                .Select(role => role.Id);

            // Shared reads bypass the tenant filter only after applying an explicit consent scope.
            return context.ClinicalRecords.IgnoreQueryFilters().Where(record =>
                record.OrganizationId == organizationId && roleIds.Contains(record.PractitionerRoleId)
                || record.OrganizationId != organizationId && record.IsPatientVisible && record.CreatedDate.HasValue
                    && context.RecordSharingRequests.Any(request => request.PatientId == record.PatientId
                        && request.ReceivingOrganizationId == organizationId && request.Status == RecordSharingStatus.Approved
                        && request.ExpiresAt > now && request.ApprovedAt.HasValue && roleIds.Contains(request.PractitionerRoleId)
                        && record.CreatedDate <= request.ApprovedAt && record.VisitDate <= request.ApprovedAt
                        && (!request.RecordFromDate.HasValue || DateOnly.FromDateTime(record.VisitDate) >= request.RecordFromDate)
                        && (!request.RecordToDate.HasValue || DateOnly.FromDateTime(record.VisitDate) <= request.RecordToDate)))
                .Include(record => record.Prescriptions);
        }

        var ownPatientIds =
            from patient in context.Patients
            join user in context.Users on patient.PersonId equals user.PersonId
            where user.Id == callerId
            select patient.Id;

        return context.ClinicalRecords
            .Where(record => (isPractitionerRole || record.IsPatientVisible)
                && (isPractitionerRole || ownPatientIds.Contains(record.PatientId)))
            .Include(record => record.Prescriptions);
    }

    public async Task<ClinicalRecord?> FindReadable(Guid id, string callerId, bool isPractitionerRole)
    {
        var record = await Readable(callerId, isPractitionerRole).FirstOrDefaultAsync(row => row.Id == id);
        if (record != null) LogSharedRead([record], callerId);
        return record;
    }

    public async Task<List<ClinicalRecord>> GetForPatient(Guid patientId, string callerId, bool isPractitionerRole)
    {
        var records = await Readable(callerId, isPractitionerRole).Where(row => row.PatientId == patientId).ToListAsync();
        LogSharedRead(records, callerId);
        return records;
    }

    private void LogSharedRead(List<ClinicalRecord> records, string callerId)
    {
        if (context.Access.Mode == WorkspaceModes.Work)
            foreach (var record in records.Where(row => row.OrganizationId != context.Access.OrganizationId))
                logger?.LogInformation("Shared clinical record {RecordId} read by account {UserId} in organization {OrganizationId}.", record.Id, callerId, context.Access.OrganizationId);
    }

    public async Task<List<ViewClinicalRecordDto>> Views(IEnumerable<ClinicalRecord> source)
    {
        var rows = source.ToList();
        var patientIds = rows.Select(row => row.PatientId).Distinct().ToArray();
        var patientQuery =
            from patient in context.Patients
            join person in context.People on patient.PersonId equals person.Id
            where patientIds.Contains(patient.Id)
            select new { patient.Id, Name = person.FirstName + " " + person.LastName };

        var patients = await patientQuery.ToDictionaryAsync(patient => patient.Id, patient => patient.Name);

        var roleIds = rows.Select(row => row.PractitionerRoleId).Distinct().ToArray();
        var practitionerQuery =
            from role in context.PractitionerRoles.IgnoreQueryFilters()
            join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join person in context.People on practitioner.PersonId equals person.Id
            where roleIds.Contains(role.Id)
            select new { role.Id, Name = person.FirstName + " " + person.LastName };

        var practitioners = await practitionerQuery.ToDictionaryAsync(practitioner => practitioner.Id, practitioner => practitioner.Name);

        var organizationIds = rows.Select(row => row.OrganizationId).Distinct().ToArray();
        var organizations = await context.Organizations.Where(row => organizationIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, row => row.Name);

        return rows.Select(row => new ViewClinicalRecordDto
        {
            Id = row.Id.ToString(), OrganizationId = row.OrganizationId,
            IsShared = context.Access.Mode == WorkspaceModes.Work && row.OrganizationId != context.Access.OrganizationId,
            IsPatientVisible = row.IsPatientVisible, PatientName = patients.GetValueOrDefault(row.PatientId)!,
            PractitionerName = practitioners.GetValueOrDefault(row.PractitionerRoleId)!, OrganizationName = organizations.GetValueOrDefault(row.OrganizationId)!,
            VisitDate = row.VisitDate, Diagnosis = row.Diagnosis, PractitionerNotes = row.PractitionerNotes, FollowUpDate = row.FollowUpDate,
            Prescriptions = row.Prescriptions?.Select(prescription => new ViewPrescriptionDto
            {
                Id = prescription.Id.ToString(), ClinicalRecordId = prescription.ClinicalRecordId.ToString(), IssuedOn = row.VisitDate,
                IssuedByPractitioner = practitioners.GetValueOrDefault(row.PractitionerRoleId)!, IssuedAtOrganization = organizations.GetValueOrDefault(row.OrganizationId)!,
                MedicineName = prescription.MedicineName, Dosage = prescription.Dosage, TimesPerDay = prescription.TimesPerDay,
                IsLongTermUse = prescription.IsLongTermUse, Duration = prescription.Duration, Notes = prescription.Notes, IsActive = prescription.IsActive
            }).ToList() ?? []
        }).ToList();
    }
    public async Task<ViewClinicalRecordDto?> View(ClinicalRecord? row) => row == null ? null : (await Views([row]))[0];

}
