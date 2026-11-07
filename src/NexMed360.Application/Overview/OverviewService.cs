using NexMed360.Application.Overview;
using NexMed360.Application.Shared;
using NexMed360Api.Interfaces.Services;
using NexMed360Api.Dtos.Overview;
using NexMed360Api.Dtos.Shared;

namespace NexMed360Api.Services;

public class OverviewService(IOverviewData data, PortalViews views) : IOverviewService
{
    private static DatewiseStatsDto Counts(PeriodCount value) => new() { Today = value.Today, ThisWeek = value.ThisWeek, ThisMonth = value.ThisMonth };
    private static CapacityDto? Capacity(CapacityRead? value) => value == null ? null : new() { TotalCapacity = value.TotalCapacity, AvailableCapacity = value.AvailableCapacity };
    public async Task<DoctorOverviewDto> GetDoctorOverview(string userId)
    {
        var row = await data.GetDoctorOverview(userId);
        return new() { TotalPatientsTreated = Counts(row.TotalPatientsTreated), PendingAppointments = row.PendingAppointments,
            UpcomingAppointments = row.UpcomingAppointments, RecentAppointments = row.RecentAppointments == null ? null : await views.Appointments(row.RecentAppointments) };
    }
    public async Task<HospitalOverviewDto> GetHospitalOverview(string userId, bool isAdmin)
    {
        var row = await data.GetHospitalOverview(userId, isAdmin);
        return new() { TotalPatients = Counts(row.TotalPatients), AvailableBeds = Capacity(row.AvailableBeds)!, StaffOnDutyToday = Capacity(row.StaffOnDutyToday),
            PendingAppointments = row.PendingAppointments, PatientTrendsOverMonths = row.PatientTrendsOverMonths,
            RecentAppointments = row.RecentAppointments == null ? null : await views.Appointments(row.RecentAppointments) };
    }
    public async Task<PatientOverviewDto> GetPatientOverview(string userId)
    {
        var row = await data.GetPatientOverview(userId);
        return new() { TotalEHRs = Counts(row.TotalEHRs), ActivePrescriptions = row.ActivePrescriptions, UpcomingAppointments = row.UpcomingAppointments,
            RecentAppointments = row.RecentAppointments == null ? null : await views.Appointments(row.RecentAppointments) };
    }
    public async Task<SystemOverviewDto> GetSystemOverview()
    {
        var row = await data.GetSystemOverview(); return new() { TotalPatients = row.TotalPatients, TotalHospitals = row.TotalHospitals,
            TotalMedicalStaff = row.TotalMedicalStaff, TotalUsers = row.TotalUsers, TotalEHRs = row.TotalEHRs };
    }
}
