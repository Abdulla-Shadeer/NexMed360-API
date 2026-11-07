using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360Api.Services;

public class ClinicalRecordRepository(ApplicationDbContext context) : IClinicalRecordRepository
{
    public async Task<ClinicalRecord> Add(ClinicalRecord record) { await context.ClinicalRecords.AddAsync(record); await context.SaveChangesAsync(); return record; }
    private IQueryable<ClinicalRecord> Readable(string callerId, bool isPractitionerRole) => context.ClinicalRecords
        .Where(row => (isPractitionerRole || row.IsPatientVisible) && (isPractitionerRole || context.Patients.Any(patient => patient.Id == row.PatientId && context.Users.Any(user => user.Id == callerId && user.PersonId == patient.PersonId)))).Include(row => row.Prescriptions);
    public Task<ClinicalRecord?> FindReadable(Guid id, string callerId, bool isPractitionerRole) => Readable(callerId, isPractitionerRole).FirstOrDefaultAsync(row => row.Id == id);
    public Task<List<ClinicalRecord>> GetForPatient(Guid patientId, string callerId, bool isPractitionerRole) => Readable(callerId, isPractitionerRole).Where(row => row.PatientId == patientId).ToListAsync();
}
