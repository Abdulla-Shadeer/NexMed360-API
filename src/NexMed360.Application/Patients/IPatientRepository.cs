using NexMed360Api.Dtos.Patient;
using NexMed360Api.Entities;

namespace NexMed360Api.Interfaces.Services;

public interface IPatientRepository
{
    Task<Patient> Add(Patient patient);
    Task<Guid?> FindPersonIdForUser(string userId);
    Task<Patient?> FindForPerson(Guid personId);
    Task<Patient> AddSelf(Patient patient);
    Task<PractitionerRole?> FindPractitionerRole(Guid roleId, string userId);
    Task<bool> PatientExists(Guid id);
    Task<Patient?> Find(Guid id);
    Task<bool> CanReadPatient(Guid id, string callerId, bool isPractitionerRole);
    Task<ViewPatientDto?> View(Patient? row);
}
