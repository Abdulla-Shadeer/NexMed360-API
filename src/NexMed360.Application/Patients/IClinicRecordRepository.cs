using NexMed360Api.Entities;
namespace NexMed360Api.Interfaces.Services;
public interface IClinicRecordRepository
{
    Task<ClinicRecord> Add(ClinicRecord record);
    Task<ClinicRecord?> FindReadable(Guid id, string callerId, bool isDoctor);
    Task<List<ClinicRecord>> GetForPatient(Guid patientId, string callerId, bool isDoctor);
}
