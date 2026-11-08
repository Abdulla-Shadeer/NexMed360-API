using NexMed360Api.Dtos.Search;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Organization;

namespace NexMed360.Application.Search;

public interface ISearchRepository
{
    Task<List<ViewPatientDto>> Patients(InputTypes type, string input);
    Task<List<ViewOrganizationDto>> Organizations(string input);
    Task<List<ViewPractitionerRoleDto>> PractitionerRoles(string? name, string? email, string? specialty);
}
