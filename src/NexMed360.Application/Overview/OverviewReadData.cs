using NexMed360Api.Entities;
namespace NexMed360.Application.Overview;

public sealed class PeriodCount { public int Today { get; set; } public int ThisWeek { get; set; } public int ThisMonth { get; set; } }
public sealed class CapacityRead { public int TotalCapacity { get; set; } public int AvailableCapacity { get; set; } }
public class PractitionerRoleOverviewRead
{
    public PeriodCount TotalPatientsTreated { get; set; } = new PeriodCount();
    public int UpcomingAppointments { get; set; } = 0;
    public int PendingAppointments { get; set; } = 0;
    public List<Appointment>? RecentAppointments { get; set; } = [];
}

public class OrganizationOverviewRead
{
    public PeriodCount TotalPatients { get; set; } = new PeriodCount();
    public CapacityRead AvailableBeds { get; set; } = new CapacityRead();
    public CapacityRead? StaffOnDutyToday { get; set; }
    public int? PendingAppointments { get; set; }

    // ["Jan": 256, "Feb": 123]
    public List<KeyValuePair<string, int>>? PatientTrendsOverMonths { get; set; }

    public List<Appointment>? RecentAppointments { get; set; }
}

public class PatientOverviewRead
{
    public PeriodCount TotalEHRs { get; set; } = new PeriodCount();
    public int? ActivePrescriptions { get; set; } = 0;
    public int? UpcomingAppointments { get; set; } = 0;
    public List<Appointment>? RecentAppointments { get; set; }
}

public class SystemOverviewRead
{
    public int TotalPatients { get; set; } = 0;
    public int TotalOrganizations { get; set; } = 0;
    public int TotalMedicalStaff { get; set; } = 0;
    public int TotalUsers { get; set; } = 0;
    public int TotalEHRs { get; set; } = 0;
}
public interface IOverviewData
{
    Task<PractitionerRoleOverviewRead> GetPractitionerRoleOverview(string userId);
    Task<OrganizationOverviewRead> GetOrganizationOverview(string userId, bool isAdmin);
    Task<PatientOverviewRead> GetPatientOverview(string userId);
    Task<SystemOverviewRead> GetSystemOverview();
}
