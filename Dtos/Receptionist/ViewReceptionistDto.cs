using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Dtos.Receptionist;

public class ViewReceptionistDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string? Location { get; set; }
    public string? Department { get; set; }
    public string HospitalId { get; set; }
    public string HospitalName { get; set; }
}
