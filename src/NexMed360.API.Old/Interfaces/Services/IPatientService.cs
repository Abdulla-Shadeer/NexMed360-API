using NexMed360Api.Common;
using NexMed360Api.Dtos.ClinicRecord;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.User;

namespace NexMed360Api.Interfaces.Services
{
    public interface IPatientService
    {
        public Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto);
        public Task<Result<ViewPatientDto>> GetPatient(string id);
        public Task<Result<ViewClinicRecordDto>> AddToClinicRecords(AddClinicRecordDto dto, string doctorUserId);
        public Task<Result<ViewClinicRecordDto>> ViewClinicRecord(string id);
        public Task<Result<List<ViewClinicRecordDto>>> ViewAllClinicRecords(string patientId);
    }
}
