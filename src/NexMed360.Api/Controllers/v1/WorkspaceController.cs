using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexMed360.Application.Access;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;

namespace NexMed360Api.Controllers.v1;

[ApiController, ApiVersion(1.0), Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class WorkspaceController(AccessService service, WorkspaceService workspaces, WorkspaceAccess access) : ControllerBase
{
    // Ok accepts object, so it needs an explicit Result wrapper.
    [HttpGet]
    public async Task<ActionResult<Result<List<WorkspaceOptionDto>>>> Get() =>
        Ok(Result<List<WorkspaceOptionDto>>.Success(await workspaces.List(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));

    [HttpGet("Current"), Workspace(WorkspaceModes.Patient + "," + WorkspaceModes.Work + "," + WorkspaceModes.Platform)]
    public ActionResult<Result<WorkspaceDto>> Current() => Ok(Result<WorkspaceDto>.Success(WorkspaceDto.From(access.Current!)));

    [HttpGet("Permissions")]
    public ActionResult<Result<IReadOnlyList<string>>> PermissionList() => Ok(Result<IReadOnlyList<string>>.Success(Permissions.All));

    [HttpGet("Roles"), Workspace(WorkspaceModes.Work, Permissions.RolesManage)]
    public async Task<ActionResult<Result<List<AccessRoleDto>>>> Roles() => Ok(Result<List<AccessRoleDto>>.Success(await service.Roles()));

    [HttpPost("Roles"), Workspace(WorkspaceModes.Work, Permissions.RolesManage)]
    public async Task<ActionResult<Result<AccessRoleDto>>> CreateRole(SaveAccessRoleDto dto) => Handle(await service.SaveRole(null, dto));

    [HttpPut("Roles/{id:guid}"), Workspace(WorkspaceModes.Work, Permissions.RolesManage)]
    public async Task<ActionResult<Result<AccessRoleDto>>> UpdateRole(Guid id, SaveAccessRoleDto dto) => Handle(await service.SaveRole(id, dto));

    [HttpGet("Members"), Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
    public async Task<ActionResult<Result<List<MembershipDto>>>> Members() => Ok(Result<List<MembershipDto>>.Success(await service.Members()));

    [HttpGet("MemberRoles"), Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
    public async Task<ActionResult<Result<List<AccessRoleDto>>>> MemberRoles() => Ok(Result<List<AccessRoleDto>>.Success(await service.Roles()));

    [HttpGet("Locations"), Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
    public async Task<ActionResult<Result<List<LocationDto>>>> Locations() => Ok(Result<List<LocationDto>>.Success(await service.Locations()));

    [HttpPut("Members/{id:guid}"), Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
    public async Task<ActionResult<Result<MembershipDto>>> UpdateMember(Guid id, UpdateMembershipDto dto) => Handle(await service.UpdateMember(id, dto));

    private ActionResult<Result<T>> Handle<T>(Result<T> result) => StatusCode(result.IsSuccess ? 200 : result.Error!.HttpStatus, result);
}
