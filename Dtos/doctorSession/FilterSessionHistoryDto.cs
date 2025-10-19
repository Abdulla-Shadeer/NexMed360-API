using FinalProjectApi.Entities.Enums;

namespace FinalProjectApi.Dtos.doctorSession;

public class FilterSessionHistoryDto
{
    public string? SessionId { get; set; }
    public string? DoctorId { get; set; }
    public string? HospitalId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public SessionStatus? Status { get; set; }
}
