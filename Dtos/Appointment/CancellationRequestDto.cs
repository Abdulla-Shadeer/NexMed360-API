using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Appointment;

public class CancellationRequestDto
{
    [Required]
    public string AppointmentId { get; set; }

    [Required]
    public string Reason { get; set; }
}
