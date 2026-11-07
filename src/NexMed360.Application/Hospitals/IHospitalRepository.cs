using NexMed360Api.Entities;

namespace NexMed360Api.Interfaces.Services;

public interface IHospitalRepository
{
    Task<bool> Exists(Guid id);
    Task<Hospital?> Find(Guid id);
    Task<Hospital> Add(Hospital hospital);
    Task<Hospital> Save(Hospital hospital);
    Task<List<Hospital>> GetAll();
    Task<bool> IsAdmin(Guid hospitalId, string userId);
    Task<(List<Doctor> Doctors, List<Receptionist> Receptionists)> GetMembers(Guid hospitalId);
}
