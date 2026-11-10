using NexMed360.Application.Access;
using NexMed360Api.Entities;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Organization;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;
namespace NexMed360Api.Services;

public class OrganizationService(IOrganizationRepository orgRepo, IAuthService auth, IRegistrationTransactions transactions, IPractitionerRoleRepository practitionerRoles, IOrganizationMemberRepository memberRepository, WorkspaceAccess? workspace = null) : IOrganizationService
{
    public async Task<Result<ViewUserDto>> AssignAdmin(CreateOrganizationManagerDto dto)
    {
        if (workspace?.IsSelected == true && workspace.Mode != WorkspaceModes.Platform)
            return ApiError.From(ErrorCode.SYS_Unauthorized, "Choose platform administration to assign an administrator.", 403);

        if (!Guid.TryParse(dto.OrganizationId, out var organizationId)) return ErrorCode.SYS_InvalidGuid;

        workspace?.RequireOrganization(organizationId);
        if (!CanManageMembers()) return MemberManagementDenied();

        if (!await orgRepo.Exists(organizationId)) return ErrorCode.HOS_NotFound;

        await using var transaction = await transactions.BeginAsync();

        var registerationResult = await auth.RegisterUser(dto.PersonalInformation, UserRoles.ORGANIZATION_MANAGER);
        if (!registerationResult.IsSuccess) return registerationResult;
        if (dto.Permissions == null || dto.Permissions.Any(permission => !Permissions.All.Contains(permission)))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Choose permissions from the list.");

        var memberError = await AddMember(
            new OrganizationMember { OrganizationId = organizationId, UserId = registerationResult.Data!.Id },
            $"Administrator access ({registerationResult.Data.Id})",
            [Permissions.OrganizationsManage, Permissions.MembersManage, Permissions.RolesManage,
                Permissions.SchedulesManage, .. dto.Permissions],
            updateExistingRole: true);
        if (memberError != null) return memberError;

        await transaction.CommitAsync();
        return registerationResult;
    }

    public async Task<Result<ViewUserDto>> AddOrganizationMember(CreateOrganizationMemberDto dto)
    {
        if (!Guid.TryParse(dto.OrganizationId, out var organizationId)) return ErrorCode.SYS_InvalidGuid;

        workspace?.RequireOrganization(organizationId);
        if (!CanManageMembers()) return MemberManagementDenied();

        if (!await orgRepo.Exists(organizationId)) return ErrorCode.HOS_NotFound;

        var locationError = await ValidateStaffLocation(organizationId, dto.LocationId);
        if (locationError != null) return locationError;

        await using var transaction = await transactions.BeginAsync();

        var registrationResult = await auth.RegisterUser(dto.PersonalInformation, UserRoles.ORGANIZATION_MEMBER);
        if (!registrationResult.IsSuccess) return registrationResult;

        var memberError = await AddMember(
            new OrganizationMember { OrganizationId = organizationId, UserId = registrationResult.Data!.Id, LocationId = dto.LocationId },
            "Front office", [Permissions.PatientsRead, Permissions.PatientsRegister, Permissions.AppointmentsManage]);
        if (memberError != null) return memberError;

        await transaction.CommitAsync();
        return registrationResult;
    }
    public async Task<Result<ViewUserDto>> AddPractitionerRole(CreatePractitionerRoleDto dto)
    {
        if (!Guid.TryParse(dto.OrganizationId, out var organizationId)) return ErrorCode.SYS_InvalidGuid;
        workspace?.RequireOrganization(organizationId);
        if (!CanManageMembers()) return MemberManagementDenied();

        if (!await orgRepo.Exists(organizationId)) return ErrorCode.HOS_NotFound;

        var locationError = await ValidateStaffLocation(organizationId, dto.LocationId);
        if (locationError != null) return locationError;

        await using var transaction = await transactions.BeginAsync();

        var registrationResult = await auth.RegisterUser(dto.PersonalInformation, UserRoles.PRACTITIONER);
        if (!registrationResult.IsSuccess) return registrationResult;

        var memberError = await AddMember(
            new OrganizationMember { OrganizationId = organizationId, UserId = registrationResult.Data!.Id, LocationId = dto.LocationId },
            "Clinician", [Permissions.PatientsRead, Permissions.AppointmentsManage, Permissions.SchedulesManage,
                Permissions.EncountersStart, Permissions.ClinicalRecordsRead, Permissions.ClinicalRecordsWrite, Permissions.PrescriptionsIssue]);
        if (memberError != null) return memberError;

        var practitioner = await practitionerRoles.FindOrAddPractitioner(Guid.Parse(registrationResult.Data.PersonId));
        await practitionerRoles.Add(
            new PractitionerRole { OrganizationId = organizationId, PractitionerId = practitioner.Id, LocationId = dto.LocationId, RoleName = dto.RoleName, Specialty = dto.Specialty ?? "" });

        await transaction.CommitAsync();
        return registrationResult;
    }
    public async Task<Result<ViewOrganizationDto>> Add(CreateOrganizationDto dto) => await orgRepo.View(await orgRepo.Add(new Organization { Name = dto.Name, Type = dto.Type, Street = dto.Street, City = dto.City, District = dto.District, Phone = dto.Phone, Grade = dto.Grade, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow }));

    public async Task<Result<List<ViewOrganizationDto>>> GetAll() => await orgRepo.Views(await orgRepo.GetAll());

    public async Task<Result<ViewOrganizationDto>> Update(UpdateOrganizationDto dto)
    {
        if (!Guid.TryParse(dto.Id, out var id)) return ApiError.From(ErrorCode.SYS_InvalidGuid, "Organization ID has invalid format!");

        workspace?.RequireOrganization(id);

        var organization = await orgRepo.Find(id);
        if (organization == null) return ErrorCode.HOS_NotFound;
        organization.Update(dto.Name, dto.Street, dto.City, dto.District, dto.Phone, dto.Grade);
        organization.Type = dto.Type ?? organization.Type;

        return await orgRepo.View(await orgRepo.Save(organization));
    }

    public async Task<Result<ViewOrganizationDto>> FindById(string id, bool isPlatformAdmin)
    {
        if (!Guid.TryParse(id, out var organizationId)) return ApiError.From(ErrorCode.SYS_InvalidGuid, "Organization ID has invalid format!");
        workspace?.RequireOrganization(organizationId);
        return await orgRepo.View(await orgRepo.Find(organizationId));
    }
    public async Task<Result<List<ViewUserDto>>> GetMembers(string id, string userId)
    {
        if (!Guid.TryParse(id, out var organizationId)) return ApiError.From(ErrorCode.SYS_InvalidGuid, "Invalid organization ID format.");

        workspace?.RequireOrganization(organizationId);
        if (!await orgRepo.Exists(organizationId)) return ApiError.From(ErrorCode.HOS_NotFound, $"Organization with ID {id} not found!");

        if (!await orgRepo.HasPermission(organizationId, userId, Permissions.MembersManage)) return ApiError.From(ErrorCode.SYS_Unauthorized, "Access denied, only authorized members of this organization can view its members.");

        return await orgRepo.MemberViews(organizationId);
    }

    private bool CanManageMembers() => workspace?.IsSelected != true
        || workspace.Mode == WorkspaceModes.Platform || workspace.Has(Permissions.MembersManage);

    private static ApiError MemberManagementDenied() => ApiError.From(ErrorCode.SYS_Unauthorized,
        "You need permission to manage staff.", 403);

    private async Task<ApiError?> AddMember(OrganizationMember requested, string roleName,
        IReadOnlyCollection<string> permissions, bool updateExistingRole = false)
    {
        var member = await memberRepository.Find(requested.OrganizationId, requested.UserId);
        var isNewMember = member == null;
        if (member != null && !updateExistingRole)
        {
            if (workspace?.HasLocationScope == true &&
                (!member.LocationId.HasValue || !workspace.LocationIds.Contains(member.LocationId.Value)))
                return ApiError.From(ErrorCode.SYS_Unauthorized, "This person's staff access is outside your location scope.", 403);

            // Existing staff keep their scope. New assignments must fit inside it.
            if (member.LocationId.HasValue &&
                !await IsWithinMemberLocation(requested.OrganizationId, member.LocationId.Value, requested.LocationId))
                return ApiError.From(ErrorCode.SYS_Unauthorized, "Choose a location within this person's existing staff access, or update their staff access first.", 403);
        }
        member ??= requested;

        var role = await memberRepository.FindRole(requested.OrganizationId, roleName);
        var isNewRole = role == null;
        if (isNewRole && workspace?.HasLocationScope == true)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Ask an organization administrator to set up this staff access role first.");

        role ??= new AccessRole { OrganizationId = requested.OrganizationId, Name = roleName };

        // Staff creation keeps shared role grants. Administrator assignment may replace existing grants.
        if (isNewRole || updateExistingRole) role.Permissions = permissions.Distinct().ToList();

        await memberRepository.Save(member, role, isNewMember, isNewRole);
        return null;
    }

    private async Task<ApiError?> ValidateStaffLocation(Guid organizationId, Guid? locationId)
    {
        if (workspace?.HasLocationScope == true &&
            (!locationId.HasValue || !workspace.LocationIds.Contains(locationId.Value)))
            return ApiError.From(ErrorCode.SYS_Unauthorized, "Choose a location within your workspace.", 403);

        if (!locationId.HasValue) return null;

        var location = await memberRepository.FindLocation(organizationId, locationId.Value);
        if (location == null || !location.IsActive)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Choose an active location in this organization.");

        return null;
    }

    private async Task<bool> IsWithinMemberLocation(Guid organizationId, Guid memberLocationId, Guid? locationId)
    {
        var visited = new HashSet<Guid>();
        // Follow parent locations; stop if the stored tree contains a cycle.
        while (locationId.HasValue && visited.Add(locationId.Value))
        {
            if (locationId.Value == memberLocationId) return true;
            var location = await memberRepository.FindLocation(organizationId, locationId.Value);
            locationId = location?.ParentId;
        }

        return false;
    }
}
