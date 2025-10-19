using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.doctorSession;

public class FilterSessionsForCurrentDayDto
{
    [Required]
    public string Date { get; set; }
    [Required]
    public string DoctorId { get; set; }
    [Required]
    public string HospitalId { get; set; }
}
