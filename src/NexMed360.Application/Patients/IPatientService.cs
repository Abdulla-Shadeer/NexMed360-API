using NexMed360Api.Common;
using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.User;

namespace NexMed360Api.Interfaces.Services
{
    public interface IPatientService
    {
        public Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto);
        public Task<Result<ViewPatientDto>> GetPatient(string id);
        public Task<Result<ViewClinicalRecordDto>> AddToClinicalRecords(AddClinicalRecordDto dto, string practitionerRoleUserId);
        public Task<Result<ViewClinicalRecordDto>> ViewClinicalRecord(string id, string? requesterUserId, bool isPractitionerRole);
        public Task<Result<List<ViewClinicalRecordDto>>> ViewAllClinicalRecords(string patientId, string? requesterUserId, bool isPractitionerRole);
    }
}
