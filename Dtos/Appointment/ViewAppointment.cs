namespace FinalProjectApi.Dtos.Appointment;

public class ViewAppointment
{
    public string PatientId { get; set; }
    public string DoctorId { get; set; }
    public string AppointmentDateTime { get; set; }
    public string Duration { get; set; }
    public string? Remarks { get; set; }
    public string Location { get; set; }
    public string Status { get; set; }
}