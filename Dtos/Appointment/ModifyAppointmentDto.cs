using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Appointment;

public class ModifyAppointmentDto
{
    [Required]
    public Guid Id { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? SlotId { get; set; } 
    public string? Remarks { get; set; }
}
