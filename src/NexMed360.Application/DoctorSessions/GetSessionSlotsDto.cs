using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.doctorSession;

public class GetSessionSlotsDto
{
    [Required]
    public Guid SessionId { get; set; }
    [Required]
    public DateOnly Date { get; set; }
}
