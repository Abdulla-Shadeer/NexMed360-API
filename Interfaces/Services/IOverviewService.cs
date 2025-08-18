using FinalProjectApi.Dtos.Overview;

namespace FinalProjectApi.Interfaces.Services;

public interface IOverviewService
{
    public Task<SystemOverviewDto> GetSystemOverview();
    public Task<HospitalOverviewDto> GetHospitalOverview(string userId, bool isAdmin);
    public Task<DoctorOverviewDto> GetDoctorOverview(string userId);
    public Task<PatientOverviewDto> GetPatientOverview(string userId);
}
