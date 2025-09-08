using FinalProjectApi.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Appointment;

public class CreateDoctorSessionDto
{
    [Required]
    public Guid DoctorId { get; set; }
    [Required]
    public TimeOnly StartTime { get; set; }
    [Required]
    public TimeOnly EndTime { get; set; }
    [Required]
    public Guid HospitalId { get; set; }
    public int? SlotDurationMinutes { get; set; }
    [Required]
    public List<SessionOccurrence> Occurrences { get; set; }
}
