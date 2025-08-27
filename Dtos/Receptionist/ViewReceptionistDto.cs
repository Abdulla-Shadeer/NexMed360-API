using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Dtos.Receptionist;

public class ViewReceptionistDto
{
    public string Id { get; set; }
    //public ViewUserDto User { get; set; }
    public string? Location { get; set; }
    public string? Department { get; set; }
    public string HospitalId { get; set; }
    public string HospitalName { get; set; }
}
