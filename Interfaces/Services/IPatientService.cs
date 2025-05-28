using FinalProjectApi.Dtos.ClinicRecord;
using FinalProjectApi.Dtos.Patient;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IPatientService
    {
        public Task<string> CreatePatient(CreatePatientDto dto);
        public Task<ViewPatientDto> ViewPatient(string id);
        public Task<bool> AddToVisitHistory(AddToVisitHistoryDto dto);
        public Task<string> AddToClinicRecords(AddClinicRecordDto dto);
        public Task<List<ClinicRecordDto>> GetAllClinicRecords(string patientId);
    }
}
