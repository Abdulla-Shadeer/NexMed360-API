using FinalProjectApi.Entities.Enums;

namespace FinalProjectApi.Dtos.doctorSession;

public class FilterDoctorSessionDto
{
    public DateOnly? Date { get; set; }
    public string? HospitalId { get; set; }
    public string? DoctorId { get; set; }
    public SessionStatus? SessionStatus { get; set; }
}
