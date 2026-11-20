using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Organizations;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Repositories;

public sealed class LocationRepository(ApplicationDbContext context) : ILocationRepository
{
    public Task<List<Location>> List() => context.Locations.OrderBy(row => row.Name).ToListAsync();
    public Task<Location?> Find(Guid id) => context.Locations.FirstOrDefaultAsync(row => row.Id == id);
    public async Task Save(Location location, bool isNew)
    {
        if (isNew) context.Locations.Add(location);
        await context.SaveChangesAsync();
    }
}
