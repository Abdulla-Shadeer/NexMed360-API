using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Dtos.doctorSession;

public class FilterDoctorSessionDto
{
    public DateOnly? Date { get; set; }
    public string? HospitalId { get; set; }
    public string? DoctorId { get; set; }
    public SessionStatus? SessionStatus { get; set; }
}
