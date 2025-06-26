using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Dtos.Doctor;

public class ViewDoctorDto
{
    public string Id { get; set; }
    public ViewUserDto User { get; set; }
    public string Speciality { get; set; }
    public string HospitalId { get; set; }
    public string HospitalName { get; set; }
}
