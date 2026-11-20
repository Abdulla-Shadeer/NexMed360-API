using NexMed360Api.Entities;

namespace NexMed360.Application.Patients;

public interface IRecordSharingRepository
{
    Task Add(RecordSharingRequest request);
    Task<bool> PatientHasPortalAccess(Guid patientId);
    Task<RecordSharingRequest?> FindForPatient(Guid id, Guid patientId);
    Task<List<RecordSharingRequestDto>> ForPatient(Guid patientId);
    Task<List<RecordSharingRequestDto>> ForOrganization(Guid organizationId, Guid? patientId);
    Task<bool> Save(RecordSharingRequest request);
}
