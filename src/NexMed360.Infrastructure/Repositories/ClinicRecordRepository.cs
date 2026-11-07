using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360Api.Services;

public class ClinicRecordRepository(ApplicationDbContext context) : IClinicRecordRepository
{
    public async Task<ClinicRecord> Add(ClinicRecord record) { await context.ClinicRecords.AddAsync(record); await context.SaveChangesAsync(); return record; }
    private IQueryable<ClinicRecord> Readable(string callerId, bool isDoctor) => context.ClinicRecords
        .Where(row => isDoctor || context.Patients.Any(patient => patient.Id == row.PatientId && patient.UserId == callerId)).Include(row => row.Prescriptions);
    public Task<ClinicRecord?> FindReadable(Guid id, string callerId, bool isDoctor) => Readable(callerId, isDoctor).FirstOrDefaultAsync(row => row.Id == id);
    public Task<List<ClinicRecord>> GetForPatient(Guid patientId, string callerId, bool isDoctor) => Readable(callerId, isDoctor).Where(row => row.PatientId == patientId).ToListAsync();
}
