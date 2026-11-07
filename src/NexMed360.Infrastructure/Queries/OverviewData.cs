using NexMed360.Infrastructure.Data;
using NexMed360.Application.Overview;
using NexMed360Api.Common;
using NexMed360Api.Interfaces.Services;
using NexMed360Api.Data;
using NexMed360Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Entities;

namespace NexMed360Api.Services;

public class OverviewData : IOverviewData
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OverviewData> _logger;

    public OverviewData(ApplicationDbContext context, ILogger<OverviewData> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PractitionerRoleOverviewRead> GetPractitionerRoleOverview(string userId)
    {
        var practitionerRoleId = await _context.PractitionerRolesFor(userId)
            .Select(doc => doc.Id)
            .FirstOrDefaultAsync();

        var practitionerRoleAppointments = _context.Appointments
            .Where(appt => appt.PractitionerRoleId == practitionerRoleId)
            .AsQueryable();

        if (!await practitionerRoleAppointments.AnyAsync()) {
            return new PractitionerRoleOverviewRead();
        }

        var filterDates = GetFilterDates();

        var overviewDto = await practitionerRoleAppointments
            .GroupBy(appt => 1)
            .Select(group => new PractitionerRoleOverviewRead {
                TotalPatientsTreated = new PeriodCount {
                    Today = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.TodayStart && appt.AppointmentDateTime <= filterDates.TomorrowStart),

                    ThisWeek = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.ThisWeekStart && appt.AppointmentDateTime <= filterDates.Now),

                    ThisMonth = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.ThisMonthStart && appt.AppointmentDateTime <= filterDates.Now),
                },
                PendingAppointments = group.Count(appt => appt.Status == AppointmentStatus.Pending),
                UpcomingAppointments = group.Count(appt => appt.Status != AppointmentStatus.Cancelled &&
                appt.AppointmentDateTime >= filterDates.Now)
            })
            .OrderBy(dto => 1)
            .FirstOrDefaultAsync() ?? new PractitionerRoleOverviewRead();

        overviewDto.RecentAppointments = await GetRecentAppointments(practitionerRoleAppointments, filterDates.Now);

        return overviewDto;
    }

    public async Task<OrganizationOverviewRead> GetOrganizationOverview(string userId, bool isAdmin)
    {
        var organizationId = isAdmin ?
            await _context.OrganizationMemberships
            .Where(admn => admn.UserId == userId)
            .Select(admn => admn.OrganizationId)
            .FirstOrDefaultAsync() :
            await _context.OrganizationMemberships
            .Where(rcpst => rcpst.UserId == userId)
            .Select(rcpst => rcpst.OrganizationId)
            .FirstOrDefaultAsync();

        // total patients (based on appointments registered at the organization)
        var filterDates = GetFilterDates();
        var organizationAppointments = _context.Appointments
            .Where(appt => appt.OrganizationId == organizationId)
            .AsQueryable();

        var overview = await organizationAppointments
            .GroupBy(appt => 1)
            .Select(group => new OrganizationOverviewRead()
            {
                TotalPatients = new PeriodCount()
                {
                    Today = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.TodayStart && appt.AppointmentDateTime < filterDates.TomorrowStart),

                    ThisWeek = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.ThisWeekStart && appt.AppointmentDateTime < filterDates.Now),

                    ThisMonth = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.ThisMonthStart && appt.AppointmentDateTime < filterDates.Now)
                },

                // Bed and staff leave management is not yet implemented. thus, defaulted to zero
                AvailableBeds = new CapacityRead(),
                StaffOnDutyToday = isAdmin ? new CapacityRead() : null,
                PendingAppointments = isAdmin ? null : group.Count(appt => appt.Status == AppointmentStatus.Pending)
            })
            .OrderBy(dto => 1)
            .FirstOrDefaultAsync() ?? new OrganizationOverviewRead();

        if (!isAdmin)
        {
            overview.RecentAppointments = await GetRecentAppointments(organizationAppointments, filterDates.Now);
        }

        if (isAdmin)
        {
            // Need to be implemented
            overview.PatientTrendsOverMonths = [
                new KeyValuePair<string, int>("Jan", 500),
                new KeyValuePair<string, int>("Feb", 2683),
                new KeyValuePair<string, int>("Mar", 398),
                new KeyValuePair<string, int>("Apr", 495),
                new KeyValuePair<string, int>("May", 1037),
                new KeyValuePair<string, int>("Jun", 3847),
                new KeyValuePair<string, int>("Jul", 474),
                new KeyValuePair<string, int>("Aug", 957),
                new KeyValuePair<string, int>("Sep", 778),
                new KeyValuePair<string, int>("Oct", 3684),
                new KeyValuePair<string, int>("Nov", 1034),
                new KeyValuePair<string, int>("Dec", 5463)
                ];
        }

        return overview;
    }

    public async Task<PatientOverviewRead> GetPatientOverview(string userId)
    {
        var patientId = await _context.PatientsFor(userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        var filterDates = GetFilterDates();

        var ehrStats = await _context.ClinicalRecords
            .Where(cr => cr.PatientId == patientId)
            .Include(cr => cr.Prescriptions)
            .GroupBy(cr => 1)
            .Select(group => new
            {
                ActivePrescriptions = group.Sum(cr => cr.Prescriptions!.Count(prs => prs.IsActive == true)),
                TotalEHRs = new PeriodCount
                {
                    Today = group.Count(cr => cr.VisitDate >= filterDates.TodayStart && cr.VisitDate < filterDates.TomorrowStart),
                    ThisWeek = group.Count(cr => cr.VisitDate >= filterDates.ThisWeekStart && cr.VisitDate < filterDates.Now),
                    ThisMonth = group.Count(cr => cr.VisitDate >= filterDates.ThisMonthStart && cr.VisitDate < filterDates.Now)
                }
            })
            .OrderBy(obj => 1)
            .FirstOrDefaultAsync() ?? new { ActivePrescriptions = 0, TotalEHRs = new PeriodCount() };

        var patientAppointments = _context.Appointments
            .Where(appt => appt.PatientId == patientId)
            .AsQueryable();

        var upcomingAppointments = await patientAppointments
            .Where(appt => appt.AppointmentDateTime > filterDates.Now && appt.Status != AppointmentStatus.Cancelled)
            .CountAsync();

        var recentAppointments = await GetRecentAppointments(patientAppointments, filterDates.Now);

        return new PatientOverviewRead()
        {
            TotalEHRs = ehrStats.TotalEHRs,
            ActivePrescriptions = ehrStats.ActivePrescriptions,
            UpcomingAppointments = upcomingAppointments,
            RecentAppointments = recentAppointments
        };
    }

    public async Task<SystemOverviewRead> GetSystemOverview()
    {
        return new SystemOverviewRead()
        {
            TotalEHRs = await _context.ClinicalRecords.CountAsync(),
            TotalOrganizations = await _context.Organizations.CountAsync(),
            TotalMedicalStaff = await _context.OrganizationMemberships.CountAsync(),
            TotalPatients = await _context.Patients.CountAsync(),
            TotalUsers = await _context.Users.CountAsync(),
        };
    }

    private FilterDate GetFilterDates()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var currentDayOfWeek = ((int)todayStart.DayOfWeek);
        var thisWeekStart = todayStart.AddDays(-currentDayOfWeek);
        var thisMonthStart = new DateTime(todayStart.Year, todayStart.Month, 1);

        return new FilterDate
        {
            Now = now,
            TodayStart = todayStart,
            TomorrowStart = tomorrowStart,
            CurrentDayOfWeek = currentDayOfWeek,
            ThisWeekStart = thisWeekStart,
            ThisMonthStart = thisMonthStart,
        };
    }

    private async Task<List<Appointment>> GetRecentAppointments(IQueryable<Appointment> appointments, DateTime now)
    {
        return await appointments
            .Where(appt => appt.AppointmentDateTime < now && appt.Status == AppointmentStatus.Completed)
            .OrderByDescending(appt => appt.AppointmentDateTime)

            .Take(5).ToListAsync();
    }

    internal class FilterDate
    {
        public DateTime Now { get; set; }
        public DateTime TodayStart { get; set; }
        public DateTime TomorrowStart { get; set; }
        public int CurrentDayOfWeek { get; set; }
        public DateTime ThisWeekStart { get; set; }
        public DateTime ThisMonthStart { get; set; }
    };
}
