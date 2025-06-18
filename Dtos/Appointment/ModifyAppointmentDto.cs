using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Appointment;

public class ModifyAppointmentDto
{
    [Required]
    public string Id { get; set; }

    public string? PatientId { get; set; }

    public string? DoctorId { get; set; }

    public DateTime? AppointmentDateTime { get; set; }

    public TimeSpan? Duration { get; set; }
    
    public string? Location { get; set; }

    public string? Remarks { get; set; }
}
