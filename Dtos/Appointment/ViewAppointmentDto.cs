namespace FinalProjectApi.Dtos.Appointment;

public class ViewAppointmentDto
{
    public string Id { get; set; }
    public string PatientName { get; set; }
    public string PatientEmail { get; set; }
    public string PatientPhone { get; set; }
    public string DoctorName { get; set; }
    public string DoctorSpeciality { get; set; }
    public string HospitalName { get; set; }
    public string HospitalAddress { get; set; }
    public string HospitalPhone { get; set; }
    public string CreatedByUserName { get; set; }
    public string AppointmentDateTime { get; set; }
    public string? Remarks { get; set; }
    public string Location { get; set; }
    public string Status { get; set; }
}