using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Appointment;

public class CreateAppointmentDto
{
    [Required]
    public string PatientId { get; set; }

    [Required]
    public string DoctorId { get; set; }
    [Required]
    public string HospitalId { get; set; }

    [Required]
    public DateTime AppointmentDateTime { get; set; }

    public string? Remarks { get; set; }
}
