using FinalProjectApi.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.doctorSession;

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
