using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.doctorSession;

public class FilterSessionsForCurrentDayDto
{
    [Required]
    public string Date { get; set; }
    [Required]
    public string DoctorId { get; set; }
    [Required]
    public string HospitalId { get; set; }
}
