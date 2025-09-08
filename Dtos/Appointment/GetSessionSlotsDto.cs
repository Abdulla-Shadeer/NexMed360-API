using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Appointment;

public class GetSessionSlotsDto
{
    [Required]
    public Guid SessionId { get; set; }
    [Required]
    public DateOnly Date { get; set; }
}
