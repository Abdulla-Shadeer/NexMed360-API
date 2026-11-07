using NexMed360.Application.Shared;
using NexMed360Api.Entities;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Organization;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;
using System.Text.Json;
namespace NexMed360Api.Services;

public class OrganizationService(IOrganizationRepository data, IAuthService auth, IRegistrationTransactions transactions, PortalViews views, IPractitionerRoleRepository practitionerRoles, IOrganizationMembershipRepository memberships) : IOrganizationService
{
    public async Task<Result<ViewUserDto>> AssignAdmin(CreateOrganizationManagerDto dto)
    {
        if (!Guid.TryParse(dto.OrganizationId, out var organizationId)) return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);
        if (!await data.Exists(organizationId)) return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.ORGANIZATION_MANAGER);
        if (!registered.IsSuccess) return registered;
        registered.Data!.OrganizationManagerProfile = await views.Admin(await memberships.Add(new OrganizationMembership { OrganizationId = organizationId, UserId = registered.Data.Id }, "Organization manager", [Permissions.OrganizationsManage, Permissions.MembersManage, Permissions.RolesManage, Permissions.SchedulesManage]));
        await transaction.CommitAsync();
        return registered;
    }
    public async Task<Result<ViewUserDto>> AddOrganizationMember(CreateOrganizationMemberDto dto)
    {
        if (!Guid.TryParse(dto.OrganizationId, out var organizationId)) return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);
        if (!await data.Exists(organizationId)) return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.ORGANIZATION_MEMBER);
        if (!registered.IsSuccess) return registered;
        var locationId = await memberships.CreateLocation(organizationId, dto.Location, dto.Department);
        registered.Data!.OrganizationMemberProfile = await views.OrganizationMembership(await memberships.Add(
            new OrganizationMembership { OrganizationId = organizationId, UserId = registered.Data.Id, LocationId = locationId },
            "Front office", [Permissions.PatientsRead, Permissions.PatientsRegister, Permissions.AppointmentsManage]));
        await transaction.CommitAsync();
        return registered;
    }
    public async Task<Result<ViewUserDto>> AddPractitionerRole(CreatePractitionerRoleDto dto)
    {
        if (!Guid.TryParse(dto.OrganizationId, out var organizationId)) return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);
        if (!await data.Exists(organizationId)) return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.PRACTITIONER);
        if (!registered.IsSuccess) return registered;
        var practitioner = await practitionerRoles.FindOrAddPractitioner(Guid.Parse(registered.Data!.PersonId));
        await memberships.Add(new OrganizationMembership { OrganizationId = organizationId, UserId = registered.Data.Id },
            "Clinician", [Permissions.PatientsRead, Permissions.AppointmentsManage, Permissions.SchedulesManage,
                Permissions.EncountersStart, Permissions.ClinicalRecordsRead, Permissions.ClinicalRecordsWrite, Permissions.PrescriptionsIssue]);
        registered.Data.PractitionerRoleProfile = await views.PractitionerRole(await practitionerRoles.Add(
            new PractitionerRole { OrganizationId = organizationId, PractitionerId = practitioner.Id, RoleName = dto.RoleName, Specialty = dto.Specialty ?? "" }));
        await transaction.CommitAsync();
        return registered;
    }
    public async Task<Result<ViewOrganizationDto>> Add(CreateOrganizationDto dto) => Result<ViewOrganizationDto>.Success(await views.Organization(await data.Add(new Organization { Name = dto.Name, Type = dto.Type, Street = dto.Street, City = dto.City, District = dto.District, Phone = dto.Phone, Grade = dto.Grade, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow })));
    public async Task<Result<List<ViewOrganizationDto>>> GetAll() => Result<List<ViewOrganizationDto>>.Success(await views.Organizations(await data.GetAll()));
    public async Task<Result<ViewOrganizationDto>> Update(UpdateOrganizationDto dto)
    {
        if (!Guid.TryParse(dto.Id, out var id)) return Result<ViewOrganizationDto>.Failure(ErrorCode.SYS_InvalidGuid, "Organization ID has invalid format!");
        var organization = await data.Find(id);
        if (organization == null) return Result<ViewOrganizationDto>.Failure(ErrorCode.HOS_NotFound);
        organization.Update(dto.Name, dto.Street, dto.City, dto.District, dto.Phone, dto.Grade);
        organization.Type = dto.Type ?? organization.Type;
        return Result<ViewOrganizationDto>.Success(await views.Organization(await data.Save(organization)));
    }
    public async Task<Result<ViewOrganizationDto>> FindById(string id, bool isPlatformAdmin)
    {
        if (!Guid.TryParse(id, out var organizationId)) return Result<ViewOrganizationDto>.Failure(ErrorCode.SYS_InvalidGuid, "Organization ID has invalid format!");
        return Result<ViewOrganizationDto>.Success(await views.Organization(await data.Find(organizationId), isPlatformAdmin));
    }
    public async Task<Result<List<ViewUserDto>>> GetMembers(string id, string userId)
    {
        if (!Guid.TryParse(id, out var organizationId)) return Result<List<ViewUserDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid organization ID format.");
        if (!await data.Exists(organizationId)) return Result<List<ViewUserDto>>.Failure(ErrorCode.HOS_NotFound, $"Organization with ID {id} not found!");
        if (!await data.IsAdmin(organizationId, userId)) return Result<List<ViewUserDto>>.Failure(ErrorCode.SYS_Unauthorized, "Access denied, only authorized members of this organization can view its members.");
        var rows = await data.GetMembers(organizationId);
        return Result<List<ViewUserDto>>.Success(await views.Members(rows));
    }
}
