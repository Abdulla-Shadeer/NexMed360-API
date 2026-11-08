using NexMed360Api.Dtos.Overview;

namespace NexMed360.Application.Overview;

public interface IOverviewRepository
{
    Task<PractitionerRoleOverviewDto> GetPractitionerRoleOverview(string userId);
    Task<OrganizationOverviewDto> GetOrganizationOverview(string userId, bool isAdmin);
    Task<PatientOverviewDto> GetPatientOverview(string userId);
    Task<SystemOverviewDto> GetSystemOverview();
}
