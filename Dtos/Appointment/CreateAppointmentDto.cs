using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Appointment;

public class CreateAppointmentDto
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public Guid SessionSlotId { get; set; }

    public string? Remarks { get; set; }
}
