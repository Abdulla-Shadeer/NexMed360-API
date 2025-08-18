using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Dtos.Shared;

namespace FinalProjectApi.Dtos.Overview;

public class DoctorOverviewDto: OverviewDto
{
    public override string Name { get; } = "Doctor";
    public DatewiseStatsDto TotalPatientsTreated { get; set; } = new DatewiseStatsDto();
    public int UpcomingAppointments { get; set; } = 0;
    public int PendingAppointments { get; set; } = 0;
    public List<ViewAppointmentDto>? RecentAppointments { get; set; } = [];
}
