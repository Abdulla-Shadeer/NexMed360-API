using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Dtos.Shared;

namespace NexMed360Api.Dtos.Overview;

public class PractitionerRoleOverviewDto: OverviewDto
{
    public override string Name { get; } = "PractitionerRole";
    public DatewiseStatsDto TotalPatientsTreated { get; set; } = new DatewiseStatsDto();
    public int UpcomingAppointments { get; set; } = 0;
    public int PendingAppointments { get; set; } = 0;
    public List<ViewAppointmentDto>? RecentAppointments { get; set; } = [];
}
