using NexMed360Api.Dtos.User;

namespace NexMed360Api.Dtos.OrganizationMember;

public class ViewOrganizationMemberDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string? Location { get; set; }
    public string? Department { get; set; }
    public string OrganizationId { get; set; }
    public string OrganizationName { get; set; }
}
