using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Entities;

namespace NexMed360.Application.Access;

public sealed class AccessService(IAccessRepository repository, WorkspaceAccess access)
{
    public async Task<List<AccessRoleDto>> Roles() =>
        [.. (await repository.Roles()).Select(row => new AccessRoleDto(row.Id, row.Name, row.Permissions))];
    public Task<List<OrganizationMemberDto>> Members() => repository.Members();
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

        if (await repository.RoleNameExists(access.OrganizationId!.Value, name, id))
            return ApiError.From(ErrorCode.SYS_BadRequest, "This role name is already in use.");

        role.Name = name;
        role.Permissions = dto.Permissions.Distinct().ToList();

        await repository.SaveRole(role, !id.HasValue);

        return new AccessRoleDto(role.Id, role.Name, role.Permissions);
    }

    public async Task<Result<OrganizationMemberDto>> UpdateMember(Guid id, UpdateOrganizationMemberDto dto)
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
        var organization = await repository.FindOrganization(organizationId);
        if (organization == null) return ErrorCode.HOS_NotFound;

        var members = await repository.Members(organizationId);
        var roles = await repository.Roles(organizationId);
        var administrators = new List<OrganizationAdministratorDto>();

        foreach (var member in members)
        {
            var permissions = roles.Where(role => member.AccessRoleIds.Contains(role.Id))
                .SelectMany(role => role.Permissions).Distinct().OrderBy(value => value).ToList();

            // Administrative grants identify administrators; a job title does not.
            if (permissions.Any(Permissions.Administration.Contains))
                administrators.Add(new(member.Id, member.Name, member.Email, member.IsActive, permissions));
        }

        return new OrganizationAdministratorsDto(organizationId, organization.Name, administrators);
    }

    public async Task<Result<string>> UpdateAdministrator(Guid organizationId, Guid organizationMemberId, UpdateOrganizationAdministratorDto dto)
    {
        if (access.Mode != WorkspaceModes.Platform) return Denied();

        if (dto.Permissions == null || dto.Permissions.Any(value => !Permissions.All.Contains(value))
            || !dto.Permissions.Any(Permissions.Administration.Contains))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Keep at least one organization administration permission and choose other permissions from the list.");

        var member = await repository.FindMember(organizationMemberId);
        if (member == null || member.OrganizationId != organizationId) return AdministratorNotFound();

        var roles = await repository.MemberRoles(organizationId, organizationMemberId);
        if (!roles.Any(IsAdministratorRole)) return AdministratorNotFound();

        AccessRole? privateRole = null;
        // Editing one administrator must not change a role used by other staff.
        foreach (var role in roles.Where(IsAdministratorRole))
        {
            if (!await repository.RoleIsShared(role.Id, organizationMemberId))
            {
                privateRole = role;
                break;
            }
        }

        var isNewRole = privateRole == null;
        privateRole ??= await CreateAdministratorRole(organizationId, member.UserId);
        privateRole.Permissions = dto.Permissions.Distinct().ToList();
        member.IsActive = dto.IsActive;

        await repository.SaveAdministrator(member, privateRole, isNewRole);

        return "Administrator access updated.";
    }

    public async Task<Result<string>> RemoveAdministrator(Guid organizationId, Guid organizationMemberId)
    {
        if (access.Mode != WorkspaceModes.Platform) return Denied();

        var member = await repository.FindMember(organizationMemberId);
        if (member == null || member.OrganizationId != organizationId) return AdministratorNotFound();

        var roles = await repository.MemberRoles(organizationId, organizationMemberId);
        if (!roles.Any(IsAdministratorRole)) return AdministratorNotFound();

        await repository.RemoveMember(member);

        return "Administrator removed from this organization. The shared account has been kept.";
    }

    public async Task<Result<string>> DeleteOrganization(Guid organizationId)
    {
        if (access.Mode != WorkspaceModes.Platform) return Denied();

        var organization = await repository.FindOrganization(organizationId);
        if (organization == null) return ErrorCode.HOS_NotFound;

        if (await repository.HasPatientHistory(organizationId)) return OrganizationHasHistory();

        // A foreign-key restriction can still stop deletion if history was added meanwhile.
        if (!await repository.DeleteOrganization(organization)) return OrganizationHasHistory();
        return "Organization deleted.";
    }

    private async Task<AccessRole> CreateAdministratorRole(Guid organizationId, string userId)
    {
        var baseName = $"Administrator - {await repository.MemberName(userId)}";
        if (baseName.Length > 85) baseName = baseName[..85];

        var name = baseName;
        var suffix = 2;
        while (await repository.RoleNameExists(organizationId, name))
            name = $"{baseName} ({suffix++})";

        return new AccessRole { OrganizationId = organizationId, Name = name };
    }

    private static bool IsAdministratorRole(AccessRole role) => role.Permissions.Any(Permissions.Administration.Contains);

    private static ApiError AdministratorNotFound() => ApiError.From(ErrorCode.SYS_BadRequest,
        "Organization administrator not found.", 404);

    private static ApiError OrganizationHasHistory() => ApiError.From(ErrorCode.SYS_BadRequest,
        "This organization has patient registrations or care history and cannot be deleted.", 409);

    private static ApiError Denied() => ApiError.From(ErrorCode.SYS_Unauthorized,
        "You do not have permission to manage this access in the current workspace.", 403);
}
