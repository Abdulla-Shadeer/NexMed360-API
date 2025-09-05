using FinalProjectApi.Common;
using FinalProjectApi.Dtos.ClinicRecord;
using FinalProjectApi.Dtos.Patient;
using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IPatientService
    {
        public Task<Result<ViewUserDto>> CreatePatient(CreatePatientDto dto);
        public Task<Result<ViewUserDto>> GetPatient(string id);
        public Task<Result<ViewClinicRecordDto>> AddToClinicRecords(AddClinicRecordDto dto, string doctorUserId);
        public Task<Result<ViewClinicRecordDto>> ViewClinicRecord(string id);
        public Task<Result<List<ViewClinicRecordDto>>> ViewAllClinicRecords(string patientId);
    }
}
