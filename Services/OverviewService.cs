using FinalProjectApi.Dtos.Overview;
using FinalProjectApi.Interfaces.Services;
using FinalProjectApi.Data;
using FinalProjectApi.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Dtos.Shared;
using FinalProjectApi.Entities;

namespace FinalProjectApi.Services;

public class OverviewService : IOverviewService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<OverviewService> _logger;

    public OverviewService(ApplicationDbContext context, IMapper mapper, ILogger<OverviewService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<DoctorOverviewDto> GetDoctorOverview(string userId)
    {
        var doctorId = await _context.Doctors
            .Where(doc => doc.UserId == userId)
            .Select(doc => doc.Id)
            .FirstOrDefaultAsync();

        var doctorAppointments = _context.Appointments
            .Where(appt => appt.DoctorId == doctorId)
            .AsQueryable();

        if (!await doctorAppointments.AnyAsync()) {
            return new DoctorOverviewDto();
        }

        var filterDates = GetFilterDates();

        var overviewDto = await doctorAppointments
            .GroupBy(appt => 1)
            .Select(group => new DoctorOverviewDto {
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
            .FirstOrDefaultAsync() ?? new DoctorOverviewDto();

        overviewDto.RecentAppointments = await GetRecentAppointments(doctorAppointments, filterDates.Now);
            
        return overviewDto;
    }

    public async Task<HospitalOverviewDto> GetHospitalOverview(string userId, bool isAdmin)
    {
        var hospitalId = isAdmin ?
            await _context.HospitalAdmins
            .Where(admn => admn.UserId == userId)
            .Select(admn => admn.HospitalId)
            .FirstOrDefaultAsync() :
            await _context.Receptionists
            .Where(rcpst => rcpst.UserId == userId)
            .Select(rcpst => rcpst.HospitalId)
            .FirstOrDefaultAsync();

        // total patients (based on appointments registered at the hospital)
        var filterDates = GetFilterDates();
        var hospitalAppointments = _context.Appointments
            .Where(appt => appt.HospitalId == hospitalId)
            .AsQueryable();

        var overview = await hospitalAppointments
            .GroupBy(appt => 1)
            .Select(group => new HospitalOverviewDto()
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
            .FirstOrDefaultAsync() ?? new HospitalOverviewDto();

        if (!isAdmin)
        {
            overview.RecentAppointments = await GetRecentAppointments(hospitalAppointments, filterDates.Now);
        }

        if (isAdmin)
        {
            // Need to be implemented
            overview.PatientTrendsOverMonths = [
                new KeyValuePair<string, int>("Jan", 0),
                new KeyValuePair<string, int>("Feb", 0),
                new KeyValuePair<string, int>("Mar", 0),
                new KeyValuePair<string, int>("Apr", 0),
                new KeyValuePair<string, int>("May", 0),
                new KeyValuePair<string, int>("Jun", 0),
                new KeyValuePair<string, int>("Jul", 0),
                new KeyValuePair<string, int>("Aug", 0),
                new KeyValuePair<string, int>("Sep", 0),
                new KeyValuePair<string, int>("Oct", 0),
                new KeyValuePair<string, int>("Nov", 0),
                new KeyValuePair<string, int>("Dec", 0)
                ];
        }

        return overview;
    }

    public async Task<PatientOverviewDto> GetPatientOverview(string userId)
    {
        throw new NotImplementedException();
    }

    public async Task<SystemOverviewDto> GetSystemOverview()
    {
        throw new NotImplementedException();
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

    private async Task<List<ViewAppointmentDto>> GetRecentAppointments(IQueryable<Appointment> appointments, DateTime now)
    {
        return await appointments
            .Where(appt => appt.AppointmentDateTime < now)
            .Where(appt => appt.Status == AppointmentStatus.Completed)
            .OrderByDescending(appt => appt.AppointmentDateTime)
            .Include(appt => appt.Patient)
            .ThenInclude(pat => pat.User)
            .Include(appt => appt.Doctor)
            .ThenInclude(doc => doc.User)
            .Include(appt => appt.CreatedByUser)
            .Include(appt => appt.Hospital)
            .Take(5)
            .Select(appt => _mapper.Map<ViewAppointmentDto>(appt))
            .ToListAsync();
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
