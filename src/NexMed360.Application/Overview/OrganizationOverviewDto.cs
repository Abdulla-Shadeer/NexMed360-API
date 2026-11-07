using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Dtos.Shared;

namespace NexMed360Api.Dtos.Overview;

public class OrganizationOverviewDto : OverviewDto
{
    public override string Name { get; } = "Organization";
    public DatewiseStatsDto TotalPatients { get; set; } = new DatewiseStatsDto();
    public CapacityDto AvailableBeds { get; set; } = new CapacityDto();
    public CapacityDto? StaffOnDutyToday { get; set; }
    public int? PendingAppointments { get; set; }

    // ["Jan": 256, "Feb": 123] 
    public List<KeyValuePair<string, int>>? PatientTrendsOverMonths { get; set; }

    public List<ViewAppointmentDto>? RecentAppointments { get; set; }
}
