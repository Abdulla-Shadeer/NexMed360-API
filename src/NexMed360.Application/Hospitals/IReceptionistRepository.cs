using NexMed360Api.Entities;
namespace NexMed360Api.Interfaces.Services;
public interface IReceptionistRepository
{
    Task<Receptionist?> Add(Receptionist row);
}
