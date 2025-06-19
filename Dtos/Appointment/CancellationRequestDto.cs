using FinalProjectApi.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Appointment;

public class CancellationRequestDto
{
    [Required]
    public Guid AppointmentId { get; set; }
    [Required]
    public Guid RequestedById { get; set; }
    [Required]
    public string Reason { get; set; }
}
