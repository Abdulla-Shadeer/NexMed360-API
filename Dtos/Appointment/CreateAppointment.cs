using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Appointment;

public class CreateAppointment
{
    [Required]
    public string PatientId { get; set; }

    [Required]
    public string DoctorId { get; set; }

    [Required]
    public DateTime AppointmentDateTime { get; set; }

    [Required]
    public TimeSpan Duration { get; set; }

    [Required]
    public string Location {  get; set; }

    public string? Remarks { get; set; }
}
