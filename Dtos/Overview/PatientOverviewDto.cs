using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Dtos.Shared;

namespace FinalProjectApi.Dtos.Overview;

public class PatientOverviewDto : OverviewDto
{
    public override string Name { get; } = "Patient";
    public DatewiseStatsDto TotalEHRs { get; set; } = new DatewiseStatsDto();
    public int? ActivePrescriptions { get; set; } = 0;
    public int? UpcomingAppointments { get; set; } = 0;
    public List<ViewAppointmentDto>? RecentAppointments { get; set; }
}
