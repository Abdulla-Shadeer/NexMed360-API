using NexMed360.Application.Overview;
using NexMed360Api.Interfaces.Services;
using NexMed360Api.Dtos.Overview;

namespace NexMed360Api.Services;

public class OverviewService(IOverviewRepository repository) : IOverviewService
{
    public Task<PractitionerRoleOverviewDto> GetPractitionerRoleOverview(string userId) => repository.GetPractitionerRoleOverview(userId);
    public Task<OrganizationOverviewDto> GetOrganizationOverview(string userId, bool isAdmin) => repository.GetOrganizationOverview(userId, isAdmin);
    public Task<PatientOverviewDto> GetPatientOverview(string userId) => repository.GetPatientOverview(userId);
    public Task<SystemOverviewDto> GetSystemOverview() => repository.GetSystemOverview();
}
