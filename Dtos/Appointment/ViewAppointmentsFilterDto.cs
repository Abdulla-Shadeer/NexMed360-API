using FinalProjectApi.Entities.Enums;

namespace FinalProjectApi.Dtos.Appointment;

public class ViewAppointmentsFilterDto
{
    public string? PatientId { get; set; }
    public string? DoctorId { get; set; }
    public string? HospitalId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public AppointmentStatus? Status { get; set; }
}
