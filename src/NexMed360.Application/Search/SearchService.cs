using NexMed360.Application.Access;
using NexMed360.Application.Search;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Dtos.User;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;
public class SearchService(ISearchRepository data, WorkspaceAccess? workspace = null) : ISearchService
{
    public async Task<Result<object>> EntitySearch(EntitySearchDto dto, CallerContext user)
    {
        if (workspace?.IsSelected == true)
        {
            if (dto.Entity == EntityTypes.Patient && (workspace.Mode != WorkspaceModes.Work || !workspace.Has(Permissions.PatientsRead)))
                return ApiError.From(ErrorCode.SYS_Unauthorized, "You do not have permission to search patients.", 403);
            return dto.Entity switch
            {
                EntityTypes.Patient => await data.Patients(dto.InputType, dto.Input),
                EntityTypes.PractitionerRole => await data.PractitionerRoles(
                    dto.InputType == InputTypes.Name ? dto.Input : null, dto.InputType == InputTypes.Email ? dto.Input : null, null),
                EntityTypes.Organization => await data.Organizations(dto.Input),
                _ => ApiError.From(ErrorCode.SYS_BadRequest, "Choose a search type.")
            };
        }
        return ApiError.From(ErrorCode.SYS_Unauthorized, "Choose a workspace before searching.", 403);
    }

    public async Task<Result<List<ViewPractitionerRoleDto>>> SearchPractitionerRole(SearchPractitionerRoleDto dto)
    {
        if (String.IsNullOrEmpty(dto.Name) && String.IsNullOrEmpty(dto.Email) && String.IsNullOrEmpty(dto.Specialty))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Empty filter!");
        return await data.PractitionerRoles(dto.Name, dto.Email, dto.Specialty);
    }
}
