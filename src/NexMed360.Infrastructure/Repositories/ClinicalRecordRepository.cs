using NexMed360Api.Dtos.ClinicalRecord;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360.Infrastructure.Repositories;

public class ClinicalRecordRepository(ApplicationDbContext context) : IClinicalRecordRepository
{
    public async Task<ClinicalRecord> Add(ClinicalRecord record) { await context.ClinicalRecords.AddAsync(record); await context.SaveChangesAsync(); return record; }
    private IQueryable<ClinicalRecord> Readable(string callerId, bool isPractitionerRole) => context.ClinicalRecords
        .Where(row => (isPractitionerRole || row.IsPatientVisible) && (isPractitionerRole || context.Patients.Any(patient => patient.Id == row.PatientId && context.Users.Any(user => user.Id == callerId && user.PersonId == patient.PersonId)))).Include(row => row.Prescriptions);
    public Task<ClinicalRecord?> FindReadable(Guid id, string callerId, bool isPractitionerRole) => Readable(callerId, isPractitionerRole).FirstOrDefaultAsync(row => row.Id == id);
    public Task<List<ClinicalRecord>> GetForPatient(Guid patientId, string callerId, bool isPractitionerRole) => Readable(callerId, isPractitionerRole).Where(row => row.PatientId == patientId).ToListAsync();

    public async Task<List<ViewClinicalRecordDto>> Views(IEnumerable<ClinicalRecord> source)
    {
        var rows = source.ToList();
        var patientIds = rows.Select(row => row.PatientId).Distinct().ToArray();
        var patients = await (from patient in context.Patients join person in context.People on patient.PersonId equals person.Id
            where patientIds.Contains(patient.Id) select new { patient.Id, Name = person.FirstName + " " + person.LastName }).ToDictionaryAsync(row => row.Id, row => row.Name);
        var roleIds = rows.Select(row => row.PractitionerRoleId).Distinct().ToArray();
        var practitioners = await (from role in context.PractitionerRoles join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join person in context.People on practitioner.PersonId equals person.Id
            where roleIds.Contains(role.Id) select new { role.Id, Name = person.FirstName + " " + person.LastName }).ToDictionaryAsync(row => row.Id, row => row.Name);
        var organizationIds = rows.Select(row => row.OrganizationId).Distinct().ToArray();
        var organizations = await context.Organizations.Where(row => organizationIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, row => row.Name);
        return rows.Select(row => new ViewClinicalRecordDto
        {
            Id = row.Id.ToString(), PatientName = patients.GetValueOrDefault(row.PatientId)!,
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
