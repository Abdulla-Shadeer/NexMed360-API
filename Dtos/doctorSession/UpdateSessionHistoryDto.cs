using FinalProjectApi.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.doctorSession;

public class UpdateSessionHistoryDto
{
    [Required]
    public string SessionId { get; set; }
    [Required]
    public DateOnly Date { get; set; }
    public SessionStatus? SessionStatus { get; set; }
    public string? LastKnownAppointmentId { get; set; }
}
