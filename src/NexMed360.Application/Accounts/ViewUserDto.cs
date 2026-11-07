using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Dtos.HospitalAdmin;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.Receptionist;

namespace NexMed360Api.Dtos.User;

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
