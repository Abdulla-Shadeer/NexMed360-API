using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Dtos.HospitalAdmin;

public class ViewHospitalAdminDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string HospitalId { get; set; }
    public string HospitalName { get; set; }
}
