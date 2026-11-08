using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Entities;

namespace NexMed360.Application.Access;

public sealed class AccessService(IAccessRepository repository, WorkspaceAccess access)
{
    public async Task<List<AccessRoleDto>> Roles() =>
        [.. (await repository.Roles()).Select(row => new AccessRoleDto(row.Id, row.Name, row.Permissions))];
    public Task<List<MembershipDto>> Members() => repository.Members();
    public Task<List<LocationDto>> Locations() => repository.Locations();

    public async Task<Result<AccessRoleDto>> SaveRole(Guid? id, SaveAccessRoleDto dto)
    {
        if (!access.Has(Permissions.RolesManage) || access.HasLocationScope) return Denied();

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Enter a role name with 1 to 100 characters.");

        if (dto.Permissions == null || dto.Permissions.Any(value => !Permissions.All.Contains(value)))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Choose permissions from the list.");

        var role = id.HasValue ? await repository.FindRole(id.Value) : new AccessRole { OrganizationId = access.OrganizationId!.Value };

        if (role == null) return ApiError.From(ErrorCode.SYS_BadRequest, "Role not found.", 404);

        if (await repository.RoleNameExists(name, id))
            return ApiError.From(ErrorCode.SYS_BadRequest, "This role name is already in use.");

        role.Name = name;
        role.Permissions = dto.Permissions.Distinct().ToList();

        await repository.SaveRole(role, !id.HasValue);

        return new AccessRoleDto(role.Id, role.Name, role.Permissions);
    }

    public async Task<Result<MembershipDto>> UpdateMember(Guid id, UpdateMembershipDto dto)
    {
        if (!access.Has(Permissions.MembersManage)) return Denied();

        var member = await repository.FindMember(id);
        if (member == null) return ApiError.From(ErrorCode.SYS_BadRequest, "Member not found.", 404);

        var roles = await repository.Roles();
        var current = (await repository.Members()).First(row => row.Id == id);
        var roleIds = dto.AccessRoleIds?.Distinct().ToArray();
        if (roleIds == null || roleIds.Any(roleId => roles.All(role => role.Id != roleId)))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Choose roles from this organization.");

        if (dto.LocationId.HasValue && !(await repository.Locations()).Any(row => row.Id == dto.LocationId))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Choose a location in your workspace.");

        if (access.HasLocationScope && (!dto.LocationId.HasValue || !access.LocationIds.Contains(dto.LocationId.Value)))
            return Denied();

        member.IsActive = dto.IsActive;
        member.LocationId = dto.LocationId;
        await repository.SaveMember(member, roleIds);
        return current with { IsActive = dto.IsActive, LocationId = dto.LocationId, AccessRoleIds = roleIds.ToList() };
    }

    public async Task<Result<OrganizationAdministratorsDto>> OrganizationAdministrators(Guid organizationId)
    {
        if (access.Mode != WorkspaceModes.Platform) return Denied();
        var result = await repository.OrganizationAdministrators(organizationId);
        if (result == null) return ErrorCode.HOS_NotFound;
        return result;
    }

    public async Task<Result<string>> UpdateAdministrator(Guid organizationId, Guid membershipId, UpdateOrganizationAdministratorDto dto)
    {
        if (access.Mode != WorkspaceModes.Platform) return Denied();

        if (dto.Permissions == null || dto.Permissions.Any(value => !Permissions.All.Contains(value))
            || !dto.Permissions.Any(Permissions.Administration.Contains))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Keep at least one organization administration permission and choose other permissions from the list.");

        if (!await repository.UpdateAdministrator(organizationId, membershipId, dto))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Organization administrator not found.", 404);

        return "Administrator access updated.";
    }

    public async Task<Result<string>> RemoveAdministrator(Guid organizationId, Guid membershipId)
    {
        if (access.Mode != WorkspaceModes.Platform) return Denied();

        if (!await repository.RemoveAdministrator(organizationId, membershipId))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Organization administrator not found.", 404);

        return "Administrator removed from this organization. The shared account has been kept.";
    }

    public async Task<Result<string>> DeleteOrganization(Guid organizationId)
    {
        if (access.Mode != WorkspaceModes.Platform) return Denied();

        return await repository.DeleteOrganization(organizationId) switch
        {
            OrganizationDeletionOutcome.Deleted => "Organization deleted.",
            OrganizationDeletionOutcome.NotFound => ErrorCode.HOS_NotFound,
            _ => ApiError.From(ErrorCode.SYS_BadRequest, "This organization has patient registrations or care history and cannot be deleted.", 409)
        };
    }

    private static ApiError Denied() => ApiError.From(ErrorCode.SYS_Unauthorized,
        "You do not have permission to manage this access in the current workspace.", 403);
}
