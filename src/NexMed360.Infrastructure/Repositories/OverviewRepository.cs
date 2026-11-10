using NexMed360Api.Dtos.Overview;
using NexMed360Api.Dtos.Shared;
using NexMed360.Infrastructure.Data;
using NexMed360.Application.Overview;
using NexMed360Api.Common;
using NexMed360Api.Interfaces.Services;
using NexMed360Api.Data;
using NexMed360Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Repositories;

public class OverviewRepository(ApplicationDbContext context, IAppointmentRepository appointments) : IOverviewRepository
{
    public async Task<PractitionerRoleOverviewDto> GetPractitionerRoleOverview(string userId)
    {
        var ownRoleIds =
            from role in context.PractitionerRoles
            join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join user in context.Users on practitioner.PersonId equals user.PersonId
            where user.Id == userId
            select role.Id;

        var practitionerRoleId = context.Access.PractitionerRoleId ?? await ownRoleIds.FirstOrDefaultAsync();

        var practitionerRoleAppointments = context.Appointments
            .Where(appt => appt.PractitionerRoleId == practitionerRoleId)
            .AsQueryable();

        if (!await practitionerRoleAppointments.AnyAsync()) {
            return new PractitionerRoleOverviewDto();
        }

        var filterDates = GetFilterDates();

        var overviewDto = await practitionerRoleAppointments
            .GroupBy(appt => 1)
            .Select(group => new PractitionerRoleOverviewDto {
                TotalPatientsTreated = new DatewiseStatsDto {
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
            .FirstOrDefaultAsync() ?? new PractitionerRoleOverviewDto();

        overviewDto.RecentAppointments = await GetRecentAppointments(practitionerRoleAppointments, filterDates.Now);

        return overviewDto;
    }

    public async Task<OrganizationOverviewDto> GetOrganizationOverview(string userId, bool isAdmin)
    {
        var organizationId = context.Access.OrganizationId ?? await context.OrganizationMembers
            .Where(member => member.UserId == userId).Select(member => member.OrganizationId).FirstOrDefaultAsync();

        // total patients (based on appointments registered at the organization)
        var filterDates = GetFilterDates();
        var organizationAppointments = context.Appointments
            .Where(appt => appt.OrganizationId == organizationId)
            .AsQueryable();

        var overview = await organizationAppointments
            .GroupBy(appt => 1)
            .Select(group => new OrganizationOverviewDto()
            {
                TotalPatients = new DatewiseStatsDto()
                {
                    Today = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.TodayStart && appt.AppointmentDateTime < filterDates.TomorrowStart),

                    ThisWeek = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.ThisWeekStart && appt.AppointmentDateTime < filterDates.Now),

                    ThisMonth = group.Count(appt => appt.Status == AppointmentStatus.Completed &&
                    appt.AppointmentDateTime >= filterDates.ThisMonthStart && appt.AppointmentDateTime < filterDates.Now)
                },

                // Bed and staff leave management is not yet implemented. thus, defaulted to zero
                AvailableBeds = new CapacityDto(),
                StaffOnDutyToday = isAdmin ? new CapacityDto() : null,
                PendingAppointments = isAdmin ? null : group.Count(appt => appt.Status == AppointmentStatus.Pending)
            })
            .OrderBy(dto => 1)
            .FirstOrDefaultAsync() ?? new OrganizationOverviewDto();

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

    public async Task<PatientOverviewDto> GetPatientOverview(string userId)
    {
        var ownPatientIds =
            from patient in context.Patients
            join user in context.Users on patient.PersonId equals user.PersonId
            where user.Id == userId
            select patient.Id;

        var patientId = await ownPatientIds.FirstOrDefaultAsync();

        var filterDates = GetFilterDates();

        var ehrStats = await context.ClinicalRecords
            .Where(cr => cr.PatientId == patientId)
            .Include(cr => cr.Prescriptions)
            .GroupBy(cr => 1)
            .Select(group => new
            {
                ActivePrescriptions = group.Sum(cr => cr.Prescriptions!.Count(prs => prs.IsActive == true)),
                TotalEHRs = new DatewiseStatsDto
                {
                    Today = group.Count(cr => cr.VisitDate >= filterDates.TodayStart && cr.VisitDate < filterDates.TomorrowStart),
                    ThisWeek = group.Count(cr => cr.VisitDate >= filterDates.ThisWeekStart && cr.VisitDate < filterDates.Now),
                    ThisMonth = group.Count(cr => cr.VisitDate >= filterDates.ThisMonthStart && cr.VisitDate < filterDates.Now)
                }
            })
            .OrderBy(obj => 1)
            .FirstOrDefaultAsync() ?? new { ActivePrescriptions = 0, TotalEHRs = new DatewiseStatsDto() };

        var patientAppointments = context.Appointments
            .Where(appt => appt.PatientId == patientId)
            .AsQueryable();

        var upcomingAppointments = await patientAppointments
            .Where(appt => appt.AppointmentDateTime > filterDates.Now && appt.Status != AppointmentStatus.Cancelled)
            .CountAsync();

        var recentAppointments = await GetRecentAppointments(patientAppointments, filterDates.Now);

        return new PatientOverviewDto()
        {
            TotalEHRs = ehrStats.TotalEHRs,
            ActivePrescriptions = ehrStats.ActivePrescriptions,
            UpcomingAppointments = upcomingAppointments,
            RecentAppointments = recentAppointments
        };
    }

    public async Task<SystemOverviewDto> GetSystemOverview()
    {
        return new SystemOverviewDto()
        {
            TotalEHRs = await context.ClinicalRecords.IgnoreQueryFilters().CountAsync(),
            TotalOrganizations = await context.Organizations.CountAsync(),
            TotalMedicalStaff = await context.OrganizationMembers.CountAsync(),
            TotalPatients = await context.Patients.IgnoreQueryFilters().CountAsync(),
            TotalUsers = await context.Users.CountAsync(),
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

    private async Task<List<NexMed360Api.Dtos.Appointment.ViewAppointmentDto>> GetRecentAppointments(IQueryable<Appointment> query, DateTime now)
    {
        var rows = await query
            .Where(appt => appt.AppointmentDateTime < now && appt.Status == AppointmentStatus.Completed)
            .OrderByDescending(appt => appt.AppointmentDateTime)

            .Take(5).ToListAsync();
        return await appointments.Views(rows);
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
