using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Dtos.Shared;

namespace NexMed360Api.Dtos.Overview;

public class PatientOverviewDto : OverviewDto
{
    public override string Name { get; } = "Patient";
    public DatewiseStatsDto TotalEHRs { get; set; } = new DatewiseStatsDto();
    public int? ActivePrescriptions { get; set; } = 0;
    public int? UpcomingAppointments { get; set; } = 0;
    public List<ViewAppointmentDto>? RecentAppointments { get; set; }
}
