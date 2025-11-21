using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FinalProjectApi.Dtos.Appointment;

public class CreateAppointmentDto
{
    [Required]
    [JsonRequired]
    public Guid PatientId { get; set; }

    [Required]
    public Guid SessionSlotId { get; set; }

    public string? Remarks { get; set; }
}
