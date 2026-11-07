using NexMed360Api.Dtos.Overview;

namespace NexMed360Api.Interfaces.Services;

public interface IOverviewService
{
    public Task<SystemOverviewDto> GetSystemOverview();
    public Task<OrganizationOverviewDto> GetOrganizationOverview(string userId, bool isAdmin);
    public Task<PractitionerRoleOverviewDto> GetPractitionerRoleOverview(string userId);
    public Task<PatientOverviewDto> GetPatientOverview(string userId);
}
