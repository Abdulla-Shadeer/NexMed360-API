using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360Api.Services;
public class HospitalAdminRepository(ApplicationDbContext context) : IHospitalAdminRepository
{
    public async Task<HospitalAdmin?> Add(HospitalAdmin row)
    {
        await context.HospitalAdmins.AddAsync(row);
        await context.SaveChangesAsync();
        return await context.HospitalAdmins.FirstOrDefaultAsync(value => value.UserId == row.UserId);
    }
}
