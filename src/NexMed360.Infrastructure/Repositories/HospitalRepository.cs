using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class HospitalRepository(ApplicationDbContext context) : IHospitalRepository
{
    public Task<bool> Exists(Guid id) => context.Hospitals.AnyAsync(row => row.Id == id);
    public Task<Hospital?> Find(Guid id) => context.Hospitals.FirstOrDefaultAsync(row => row.Id == id);
    public async Task<Hospital> Add(Hospital row) { await context.Hospitals.AddAsync(row); await context.SaveChangesAsync(); return row; }
    public async Task<Hospital> Save(Hospital row) { await context.SaveChangesAsync(); return row; }
    public Task<List<Hospital>> GetAll() => context.Hospitals.ToListAsync();
    public Task<bool> IsAdmin(Guid id, string userId) => context.HospitalAdmins.AnyAsync(row => row.HospitalId == id && row.UserId == userId);
    public async Task<(List<Doctor> Doctors, List<Receptionist> Receptionists)> GetMembers(Guid id)
        => (await context.Doctors.Where(row => row.HospitalId == id).ToListAsync(), await context.Receptionists.Where(row => row.HospitalId == id).ToListAsync());
}
