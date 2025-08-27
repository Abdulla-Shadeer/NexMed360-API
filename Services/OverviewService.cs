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
        var patientId = await _context.Patients
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        var filterDates = GetFilterDates();

        var ehrStats = await _context.ClinicRecords
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

        var patientAppointments = _context.Appointments
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
            TotalEHRs = await _context.ClinicRecords.CountAsync(),
            TotalHospitals = await _context.Hospitals.CountAsync(),
            TotalMedicalStaff = (await _context.Doctors.CountAsync()) + (await _context.HospitalAdmins.CountAsync()) + (await _context.Receptionists.CountAsync()),
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

    private async Task<List<ViewAppointmentDto>> GetRecentAppointments(IQueryable<Appointment> appointments, DateTime now)
    {
        return await appointments
            .Where(appt => appt.AppointmentDateTime < now && appt.Status == AppointmentStatus.Completed)
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
