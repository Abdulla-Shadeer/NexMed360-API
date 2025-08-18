using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Dtos.Shared;

namespace FinalProjectApi.Dtos.Overview;

public class HospitalOverviewDto : OverviewDto
{
    public override string Name { get; } = "Hospital";
    public DatewiseStatsDto TotalPatients { get; set; } = new DatewiseStatsDto();
    public CapacityDto AvailableBeds { get; set; } = new CapacityDto();
    public CapacityDto? StaffOnDutyToday { get; set; }
    public int? PendingAppointments { get; set; }

    // ["Jan": 256, "Feb": 123] 
    public List<KeyValuePair<string, int>>? PatientTrendsOverMonths { get; set; }

    public List<ViewAppointmentDto>? RecentAppointments { get; set; }
}
