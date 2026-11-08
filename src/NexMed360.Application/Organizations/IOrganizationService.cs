using NexMed360Api.Common;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Organization;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.User;

namespace NexMed360Api.Interfaces.Services;

public interface IOrganizationService
{
    public Task<Result<ViewOrganizationDto>> Add(CreateOrganizationDto dto);
    public Task<Result<ViewUserDto>> AssignAdmin(CreateOrganizationManagerDto dto);
    public Task<Result<ViewUserDto>> AddOrganizationMember(CreateOrganizationMemberDto dto);
    public Task<Result<ViewUserDto>> AddPractitionerRole(CreatePractitionerRoleDto dto);
    public Task<Result<List<ViewOrganizationDto>>> GetAll();
    public Task<Result<ViewOrganizationDto>> Update(UpdateOrganizationDto updateDto);
    public Task<Result<ViewOrganizationDto>> FindById(string id, bool isPlatformAdmin);
    public Task<Result<List<ViewUserDto>>> GetMembers(string organizationId, string userId);
}
