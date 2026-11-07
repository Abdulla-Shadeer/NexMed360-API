using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.OrganizationMember;

namespace NexMed360Api.Dtos.User;

public class ViewUserDto
{
    public string PersonId { get; set; } = "";
    public List<ViewPractitionerRoleDto> PractitionerRoleProfiles { get; set; } = [];
    public List<ViewOrganizationMemberDto> Memberships { get; set; } = [];
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = [];
    public ViewPatientDto? PatientProfile { get; set; }
    public ViewPractitionerRoleDto? PractitionerRoleProfile { get; set; }
    public ViewOrganizationMemberDto? OrganizationMemberProfile { get; set; }
    public ViewOrganizationManagerDto? OrganizationManagerProfile { get;set; }
}
