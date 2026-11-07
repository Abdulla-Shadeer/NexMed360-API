using NexMed360Api.Common;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Common.Auth;

namespace NexMed360Api.Interfaces.Services;

public interface ISearchService
{
    Task<Result<List<ViewPractitionerRoleDto>>> SearchPractitionerRole(SearchPractitionerRoleDto dto);
    Task<Result<object>> EntitySearch(EntitySearchDto dto, CallerContext user);
}
