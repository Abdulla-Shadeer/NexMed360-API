using FinalProjectApi.Common;
using FinalProjectApi.Dtos.ClinicRecord;
using FinalProjectApi.Dtos.Patient;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IPatientService
    {
        public Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto);
        public Task<Result<ViewPatientDto>> ViewPatient(string id);
        public Task<Result<ViewClinicRecordDto>> AddToClinicRecords(AddClinicRecordDto dto);
        public Task<Result<ViewClinicRecordDto>> ViewClinicRecord(string id);
        public Task<Result<List<ViewClinicRecordDto>>> ViewAllClinicRecords(string patientId);
    }
}
