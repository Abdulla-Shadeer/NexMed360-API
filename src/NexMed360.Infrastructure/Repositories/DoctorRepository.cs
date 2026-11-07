using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360Api.Services;

public class DoctorRepository(ApplicationDbContext context) : IDoctorRepository
{
    public async Task<Doctor?> Add(Doctor row)
    {
        await context.Doctors.AddAsync(row);
        await context.SaveChangesAsync();
        return await context.Doctors.FirstOrDefaultAsync(value => value.UserId == row.UserId);
    }
}
