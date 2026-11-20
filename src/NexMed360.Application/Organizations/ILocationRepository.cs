using NexMed360Api.Entities;

namespace NexMed360.Application.Organizations;

public interface ILocationRepository
{
    Task<List<Location>> List();
    Task<Location?> Find(Guid id);
    Task Save(Location location, bool isNew);
}

