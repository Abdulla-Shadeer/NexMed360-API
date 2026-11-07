namespace NexMed360Api.Dtos.PractitionerRole;

public class ViewPractitionerRoleDto
{
    public string PractitionerId { get; set; } = "";
    public string RoleName { get; set; } = "";
    public string Id { get; set; }
    public string UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string Specialty { get; set; }
    public string OrganizationId { get; set; }
    public string OrganizationName { get; set; }
}
