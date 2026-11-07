using NexMed360Api.Entities;
namespace NexMed360Api.Interfaces.Services;
public interface IClinicalRecordRepository
{
    Task<ClinicalRecord> Add(ClinicalRecord record);
    Task<ClinicalRecord?> FindReadable(Guid id, string callerId, bool isPractitionerRole);
    Task<List<ClinicalRecord>> GetForPatient(Guid patientId, string callerId, bool isPractitionerRole);
}
