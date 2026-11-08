using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Entities;
namespace NexMed360Api.Interfaces.Services;
public interface IPractitionerRoleRepository
{
    Task<PractitionerRole?> Add(PractitionerRole row);
    Task<Practitioner> FindOrAddPractitioner(Guid personId);
    Task<List<ViewPractitionerRoleDto>> Views(IEnumerable<PractitionerRole> rows);
}
