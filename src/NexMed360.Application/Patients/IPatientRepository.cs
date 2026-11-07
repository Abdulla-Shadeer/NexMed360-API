using NexMed360Api.Entities;

namespace NexMed360Api.Interfaces.Services;

public interface IPatientRepository
{
    Task<Patient> Add(Patient patient);
    Task<Guid> FindDoctorId(string userId);
    Task<bool> PatientExists(Guid id);
    Task<Patient?> Find(Guid id);
    Task<bool> CanReadPatient(Guid id, string callerId, bool isDoctor);
}
