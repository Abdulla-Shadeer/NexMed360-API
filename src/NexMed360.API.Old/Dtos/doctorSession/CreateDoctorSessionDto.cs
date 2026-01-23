using NexMed360.API.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.doctorSession;

public class CreateDoctorSessionDto
{
    [Required]
    public string DoctorId { get; set; }
    [Required]
    public TimeOnly StartTime { get; set; }
    [Required]
    public TimeOnly EndTime { get; set; }
    [Required]
    public string HospitalId { get; set; }
    public int? SlotDurationMinutes { get; set; }
    [Required]
    public List<SessionOccurrence> Occurrences { get; set; }
}
