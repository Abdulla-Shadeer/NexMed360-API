using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Entities;
namespace NexMed360Api.Interfaces.Services;
public interface IClinicalRecordRepository
{
    Task<List<ClinicalRecord>> GetForExchange(Guid patientId, Guid organizationId);
    Task<ClinicalRecord?> FindForExchange(Guid id, Guid organizationId);
    Task<ClinicalRecord?> FindForPrescriptionExchange(Guid prescriptionId, Guid organizationId);
    Task<List<ClinicalRecord>> GetForAuthorExchange(string partnerId, Guid organizationId, Guid? practitionerRoleId = null, Guid? practitionerId = null);
    Task<ClinicalRecord> Add(ClinicalRecord record);
    Task<ClinicalRecord?> FindReadable(Guid id, string callerId, bool isPractitionerRole);
    Task<List<ClinicalRecord>> GetForPatient(Guid patientId, string callerId, bool isPractitionerRole);
    Task<List<ViewClinicalRecordDto>> Views(IEnumerable<ClinicalRecord> rows);
    Task<ViewClinicalRecordDto?> View(ClinicalRecord? row);
}
