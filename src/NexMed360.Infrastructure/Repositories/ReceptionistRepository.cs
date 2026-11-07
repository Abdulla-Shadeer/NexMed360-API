using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360Api.Services;

public class ReceptionistRepository(ApplicationDbContext context) : IReceptionistRepository
{
    public async Task<Receptionist?> Add(Receptionist row)
    {
        await context.Receptionists.AddAsync(row);
        await context.SaveChangesAsync();
        return await context.Receptionists.FirstOrDefaultAsync(value => value.UserId == row.UserId);
    }
}
