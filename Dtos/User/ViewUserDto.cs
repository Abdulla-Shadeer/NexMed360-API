using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Patient;
using FinalProjectApi.Dtos.Receptionist;

namespace FinalProjectApi.Dtos.User;

public class ViewUserDto
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = [];
    public ViewPatientDto? PatientProfile { get; set; }
    public ViewDoctorDto? DoctorProfile { get; set; }
    public ViewReceptionistDto? ReceptionistProfile { get; set; }
    public ViewHospitalAdminDto? HospitalAdminProfile { get;set; }
}
