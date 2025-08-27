using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Dtos.HospitalAdmin;

public class ViewHospitalAdminDto
{
    public string Id { get; set; }
    //public ViewUserDto User { get; set; }
    public string HospitalId { get; set; }
    public string HospitalName { get; set; }
}
