using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class PatientRepository(ApplicationDbContext context) : IPatientRepository
{
    public async Task<Patient> Add(Patient patient) { await context.Patients.AddAsync(patient); await context.SaveChangesAsync(); return patient; }
    public Task<Guid> FindDoctorId(string userId) => context.Doctors.Where(row => row.UserId == userId).Select(row => row.Id).FirstOrDefaultAsync();
    public Task<bool> PatientExists(Guid id) => context.Patients.AnyAsync(row => row.Id == id);
    public Task<Patient?> Find(Guid id) => context.Patients.FirstOrDefaultAsync(row => row.Id == id);
    public Task<bool> CanReadPatient(Guid id, string callerId, bool isDoctor) => context.Patients.AnyAsync(row => row.Id == id && (isDoctor || row.UserId == callerId));
}
