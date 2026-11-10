using NexMed360.Application.Accounts;
using NexMed360.Application.Access;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.User;
using NexMed360Api.Dtos.Overview;
using NexMed360Api.Interfaces.Services;
using NexMed360.Domain.Rules;

namespace NexMed360Api.Services;

public class UserService(IUserRepository users, IOverviewService overviewService, WorkspaceAccess? workspace = null) : IUserService
{
    private readonly IOverviewService _overviewService = overviewService;
    public async Task<Result<ViewUserDto>> GetUserAsync(string id)
    {
        var user = await users.GetUserAsync(id);
        if (user == null) return ApiError.From(ErrorCode.SYS_Unauthorized, $"Account with ID {id} not found.");
        return user;
    }
    public async Task<Result<ViewUserDto>> UpdateUser(UpdateUserDto dto, string? requesterUserId)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
            return ErrorCode.SYS_Unauthorized;
        if (!AccountRules.IsSelf(dto.Id, requesterUserId))
            return ApiError.From(ErrorCode.SYS_Unauthorized, "You can update only your own account.", 403);

        if (dto.Email != null && await users.EmailExists(dto.Email, requesterUserId))
            return ApiError.From(ErrorCode.SYS_DuplicateEntry, $"This email '{dto.Email}' is already used by someone.");

        var user = await users.UpdateUser(dto);
        if (user == null) return ErrorCode.USR_NotFound;
        return user;
    }

    public async Task<Result<List<OverviewDto>>> GetOverview(CallerContext user)
    {
        var userId = user.UserId;
        if (userId == null)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Please log in again.");

        if (workspace?.IsSelected == true)
        {
            List<OverviewDto> selected = [];
            if (workspace.Mode == WorkspaceModes.Platform) selected.Add(await _overviewService.GetSystemOverview());
            if (workspace.Mode == WorkspaceModes.Patient) selected.Add(await _overviewService.GetPatientOverview(userId));
            if (workspace.Mode == WorkspaceModes.Work && workspace.Has(Permissions.AppointmentsManage))
                selected.Add(await _overviewService.GetOrganizationOverview(userId, false));
            return selected;
        }
        return ApiError.From(ErrorCode.SYS_Unauthorized, "Choose a workspace to view its overview.", 403);
    }
}
