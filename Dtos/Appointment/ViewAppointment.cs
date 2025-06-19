namespace FinalProjectApi.Dtos.Appointment;

public class ViewAppointment
{
    public string Id { get; set; }
    public string PatientId { get; set; }
    public string DoctorId { get; set; }
    public string CreatedById { get; set; }
    public string AppointmentDateTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Remarks { get; set; }
    public string Location { get; set; }
    public string Status { get; set; }
}