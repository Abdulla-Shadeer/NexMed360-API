using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NexMed360.API.Dtos.doctorSession;
using NexMed360.API.Entities.Enums;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Data;
using NexMed360Api.Dtos.doctorSession;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class DoctorSessionService : IDoctorSessionService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public DoctorSessionService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<Result<ViewDoctorSessionDto>> Create(CreateDoctorSessionDto dto)
    {
        if (!Guid.TryParse(dto.DoctorId, out var doctorId))
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Doctor ID");

        if (!Guid.TryParse(dto.HospitalId, out var hospitalId))
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Hospital ID");

        if (!await _context.Doctors.AnyAsync(dr => dr.Id == doctorId))
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.APPT_DoctorNotFound);

        if (!await _context.Hospitals.AnyAsync(hs => hs.Id == hospitalId))
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.HOS_NotFound);

        if (dto.StartTime > dto.EndTime)
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.SYS_BadRequest, "Start time cannot be after End time!");

        // All sessions at the given hospital for the doctor
        var doctorSessions = await _context.DoctorSessions
            .Where(ds => ds.DoctorId == doctorId && ds.HospitalId == hospitalId)
            .ToListAsync();

        var overlapValidation = ValidateOverlaps<ViewDoctorSessionDto>(doctorSessions, dto.Occurrences, dto.StartTime, dto.EndTime);
        if (!overlapValidation.IsSuccess)
            return overlapValidation;

        var addedEntityId = (await _context.DoctorSessions.AddAsync(_mapper.Map<DoctorSession>(dto))).Entity.Id;
        await _context.SaveChangesAsync();

        var doctorSession = await _context.DoctorSessions
            .Where(ds => ds.Id == addedEntityId)
            .Include(ds => ds.Hospital)
            .Select(ds => _mapper.Map<ViewDoctorSessionDto>(ds))
            .FirstOrDefaultAsync();

        return Result<ViewDoctorSessionDto>.Success(doctorSession);
    }

    public async Task<Result<List<ViewDoctorSessionDto>>> Get(FilterDoctorSessionDto filter)
    {

        if (filter.DoctorId != null && !Guid.TryParse(filter.DoctorId, out _))
            return Result<List<ViewDoctorSessionDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Doctor ID");

        if (filter.HospitalId != null && !Guid.TryParse(filter.HospitalId, out _))
            return Result<List<ViewDoctorSessionDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Hospital ID");

        return Result<List<ViewDoctorSessionDto>>.Success(
            await _context.DoctorSessions
                .Include(ds => ds.History)
                .Where(ds =>
                    (filter.DoctorId == null || ds.DoctorId == Guid.Parse(filter.DoctorId)) &&
                    (filter.HospitalId == null || ds.HospitalId == Guid.Parse(filter.HospitalId)) &&
                    ((filter.Date == null || !filter.Date.HasValue) || AppointmentHelper.IsDateAvailable(filter.Date.Value, ds.Occurrences)) &&
                    (filter.SessionStatus == null || ds.History.Any(hr => hr.SessionStatus == filter.SessionStatus)))
                .Include(ds => ds.Hospital)
                .Select(ds => _mapper.Map<ViewDoctorSessionDto>(ds))
                .ToListAsync());
    }

    public async Task<Result<List<ViewSessionSlotDto>>> GetSlots(GetSessionSlotsDto dto)
    {
        var session = await _context.DoctorSessions.FirstOrDefaultAsync(ds => ds.Id == dto.SessionId);

        if (session == null)
        {
            return Result<List<ViewSessionSlotDto>>.Failure(ErrorCode.SYS_BadRequest, "No sessions found with given ID");
        }

        // return if slots present with given date and id
        var existingSlots = await _context.SessionSlots
            .Where(ss => ss.SessionId == dto.SessionId && ss.Date == dto.Date)
            .Select(ss => _mapper.Map<ViewSessionSlotDto>(ss))
            .ToListAsync();

        if (existingSlots != null && existingSlots.Count > 0)
        {
            return Result<List<ViewSessionSlotDto>>.Success(existingSlots);
        }

        var slotGeneration = AppointmentHelper.CheckAndGenerateSlots(session, dto.Date);

        if (!slotGeneration.IsSuccess)
        {
            return Result<List<ViewSessionSlotDto>>.Failure(slotGeneration.Error!);
        }

        await _context.SessionSlots.AddRangeAsync(slotGeneration.Data!);
        await _context.SaveChangesAsync();

        return Result<List<ViewSessionSlotDto>>.Success(
            await _context.SessionSlots
            .Where(ss => ss.SessionId == dto.SessionId && ss.Date == dto.Date)
            .Select(ss => _mapper.Map<ViewSessionSlotDto>(ss))
            .ToListAsync()
            );
    }

    public async Task<Result<ViewSessionHistoryDto>> Start(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var id))
        {
            return Result<ViewSessionHistoryDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Session ID");
        }

        if (!await _context.DoctorSessions.AnyAsync(ds => ds.Id == id))
        {
            return Result<ViewSessionHistoryDto>.Failure(ErrorCode.DRSSN_NotFound);
        }

        await _context.SessionHistories.AddAsync(new SessionHistory() { DoctorSessionId = id });
        await _context.SaveChangesAsync();

        var history = await _context.SessionHistories
            .Include(sh => sh.DoctorSession)
            .ThenInclude(ds => ds.Doctor)
            .ThenInclude(dr => dr.User)
            .Where(sh => sh.DoctorSessionId == id && sh.Date == new DateOnly(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day))
            .FirstOrDefaultAsync();

        return Result<ViewSessionHistoryDto>
            .Success(_mapper.Map<ViewSessionHistoryDto>(history));
    }

    public async Task<Result<ViewSessionHistoryDto>> UpdateHistory(UpdateSessionHistoryDto dto)
    {
        if (!Guid.TryParse(dto.SessionId, out var sessionId))
            return Result<ViewSessionHistoryDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Session ID");

        if (dto.SessionStatus != null && dto.SessionStatus == SessionStatus.Canceled)
        {
            var entity = new SessionHistory()
            {
                DoctorSessionId = sessionId,
                SessionStatus = SessionStatus.Canceled,
            };
            await _context.SessionHistories.AddAsync(entity);
            await _context.SaveChangesAsync();

            var cancelledSession = await _context.SessionHistories
                .Where(sh => sh.DoctorSessionId == sessionId && sh.Date == new DateOnly(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day))
                .Include(sh => sh.DoctorSession)
                .ThenInclude(ds => ds.Doctor)
                .ThenInclude(dr => dr.User)
                .FirstOrDefaultAsync();

            return Result<ViewSessionHistoryDto>
                .Success(_mapper.Map<ViewSessionHistoryDto>(cancelledSession));
        }

        var history = await _context.SessionHistories
            .Include(sh => sh.DoctorSession)
            .ThenInclude(ds => ds.Doctor)
            .ThenInclude(dr => dr.User)
            .FirstOrDefaultAsync(ss => ss.DoctorSessionId == sessionId && ss.Date == dto.Date);

        if (history == null)
            return Result<ViewSessionHistoryDto>.Failure(ErrorCode.DRSSN_HSTRY_NotFound);

        if (dto.SessionStatus != null)
        {
            var status = (SessionStatus)dto.SessionStatus;
            history.SessionStatus = status;

            if (status == SessionStatus.Ended)
            {
                history.EndTime = new TimeOnly(DateTime.Now.TimeOfDay.Ticks);
            }
        }

        if (dto.LastKnownAppointmentId != null)
        {
            if (Guid.TryParse(dto.LastKnownAppointmentId, out var lastKnownApptId))
            {
                if (history.LastKnownAppointmentId == null || history.LastKnownAppointmentId != lastKnownApptId)
                {
                    history.EncountersCount += 1;
                    history.LastKnownAppointmentId = lastKnownApptId;
                }
            }
            else
            {
                return Result<ViewSessionHistoryDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Appointment ID Format");
            }
        }

        await _context.SaveChangesAsync();
        return Result<ViewSessionHistoryDto>.Success(_mapper.Map<ViewSessionHistoryDto>(history));
    }

    public async Task<Result<List<ViewSessionHistoryDto>>> GetHistory(FilterSessionHistoryDto filter)
    {
        if (filter.SessionId != null && !Guid.TryParse(filter.SessionId, out _))
            return Result<List<ViewSessionHistoryDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Session ID");
        if (filter.DoctorId != null && !Guid.TryParse(filter.DoctorId, out _))
            return Result<List<ViewSessionHistoryDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Doctor ID");
        if (filter.HospitalId != null && !Guid.TryParse(filter.HospitalId, out _))
            return Result<List<ViewSessionHistoryDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Hospital ID");

        return Result<List<ViewSessionHistoryDto>>.Success(
            await _context.SessionHistories
            .Include(sh => sh.DoctorSession)
            .Where(sh =>
                (filter.SessionId == null || sh.DoctorSessionId == Guid.Parse(filter.SessionId)) &&
                (filter.DoctorId == null || sh.DoctorSession.DoctorId == Guid.Parse(filter.DoctorId)) &&
                (filter.HospitalId == null || sh.DoctorSession.HospitalId == Guid.Parse(filter.HospitalId)) &&
                (filter.Status == null || sh.SessionStatus == filter.Status) &&
                ((filter.FromDate == null || !filter.FromDate.HasValue) || sh.Date > filter.FromDate) &&
                ((filter.ToDate == null || !filter.ToDate.HasValue) || sh.Date < filter.ToDate))
            .Select(sh => _mapper.Map<ViewSessionHistoryDto>(sh))
            .ToListAsync()
            );
    }

    public async Task<Result<ViewCurrentOrNextSessionDto>> GetCurrentOrUpcoming(FilterSessionsForCurrentDayDto filter)
    {
        if (!Guid.TryParse(filter.DoctorId, out Guid doctorId))
            return Result<ViewCurrentOrNextSessionDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Doctor ID");

        if (!Guid.TryParse(filter.HospitalId, out Guid hospitalId))
            return Result<ViewCurrentOrNextSessionDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Hospital ID");

        if (!DateOnly.TryParse(filter.Date, out DateOnly date))
            return Result<ViewCurrentOrNextSessionDto>.Failure(ErrorCode.SYS_BadRequest, "Invalid Date format");

        var ongoing = await _context.SessionHistories
            .Include(sh => sh.DoctorSession)
            .Where(h => h.SessionStatus == SessionStatus.Started &&
                h.Date == date &&
                h.DoctorSession.DoctorId == doctorId &&
                h.DoctorSession.HospitalId == hospitalId)
            .FirstOrDefaultAsync();

        if (ongoing != null)
        {
            return Result<ViewCurrentOrNextSessionDto>.Success(_mapper.Map<ViewCurrentOrNextSessionDto>(ongoing));
        }

        var now = new TimeOnly(DateTime.Now.TimeOfDay.Ticks);
        var upcomingSessions = _context.DoctorSessions
            .AsEnumerable()
            .Where(ds =>
                ds.DoctorId == doctorId &&
                ds.HospitalId == hospitalId &&
                AppointmentHelper.IsDateAvailable(date, ds.Occurrences) &&
                ds.EndTime > now)
            .ToList();

        var cancelledOrEndedSessionIds = await _context.DoctorSessions
            .Include(ds => ds.History)
            .Where(ds => ds.History.Any(h => h.Date == date &&
                (h.SessionStatus == SessionStatus.Canceled || h.SessionStatus == SessionStatus.Ended)))
            .Select(ds => ds.Id)
            .ToListAsync();

        upcomingSessions.RemoveAll(s => cancelledOrEndedSessionIds.Contains(s.Id));

        return Result<ViewCurrentOrNextSessionDto>.Success(
            _mapper.Map<ViewCurrentOrNextSessionDto>(upcomingSessions.OrderBy(us => us.StartTime).FirstOrDefault()));
    }

    public Result<bool> HasAnySessionToday(FilterSessionsForCurrentDayDto filter)
    {
        if (!Guid.TryParse(filter.DoctorId, out Guid doctorId))
            return Result<bool>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Doctor ID");

        if (!Guid.TryParse(filter.HospitalId, out Guid hospitalId))
            return Result<bool>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Hospital ID");

        if (!DateOnly.TryParse(filter.Date, out DateOnly date))
            return Result<bool>.Failure(ErrorCode.SYS_BadRequest, "Invalid Date format");

        return Result<bool>.Success(
            _context.DoctorSessions
            .AsEnumerable()
            .Any(ds =>
                ds.DoctorId == doctorId &&
                ds.HospitalId == hospitalId &&
                AppointmentHelper.IsDateAvailable(date, ds.Occurrences)
                ));
    }

    public async Task<Result<List<ViewSessionHistoryDto>>> GetOngoing(string receptionistUserId)
    {
        var hospitalId = await _context.Receptionists
            .Where(r => r.UserId == receptionistUserId)
            .Select(r => r.HospitalId)
            .FirstOrDefaultAsync();

        var sessionHistories = await _context.SessionHistories
            .Include(sh => sh.DoctorSession)
                .ThenInclude(ds => ds.Doctor)
                    .ThenInclude(dr => dr.User)
            .Where(sh => sh.DoctorSession.HospitalId == hospitalId &&
                sh.SessionStatus == SessionStatus.Started &&
                sh.Date == new DateOnly(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day))
            .Select(sh => _mapper.Map<ViewSessionHistoryDto>(sh))
            .ToListAsync();

        return Result<List<ViewSessionHistoryDto>>.Success(sessionHistories);
    }

    public async Task<Result<ViewDoctorSessionDto>> Update(string sessionId, UpdateDoctorSessionDto dto)
    {
        if (!Guid.TryParse(sessionId, out var id))
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Session ID");
        if (dto.StartTime > dto.EndTime)
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.SYS_BadRequest, "Start time cannot be after End time!");

        var session = await _context.DoctorSessions.FirstOrDefaultAsync(ds => ds.Id == id);
        if (session == null)
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.DRSSN_NotFound);


        // aLL sessions at the same hospital for the same doctor (except the current one)
        var existingSessions = await _context.DoctorSessions
            .Where(ds => ds.HospitalId == session.HospitalId &&
                        ds.DoctorId == session.DoctorId &&
                        ds.Id != id)
            .ToListAsync();

        var overlapValidation = ValidateOverlaps<ViewDoctorSessionDto>(existingSessions, dto.Occurrences, dto.StartTime, dto.EndTime);
        if (!overlapValidation.IsSuccess)
            return overlapValidation;


        session.StartTime = dto.StartTime;
        session.EndTime = dto.EndTime;
        session.SlotDurationMinutes = dto.SlotDurationMinutes;
        session.Occurrences = dto.Occurrences;
        session.IsActive = dto.IsActive;
        await _context.SaveChangesAsync();

        var updatedSession = await _context.DoctorSessions
            .Where(ds => ds.Id == id)
            .Include(ds => ds.Hospital)
            .Select(ds => _mapper.Map<ViewDoctorSessionDto>(ds))
            .FirstOrDefaultAsync();
        return Result<ViewDoctorSessionDto>.Success(updatedSession);
    }

    public async Task<Result<string>> Delete(string sessionId) 
    {
        if (!Guid.TryParse(sessionId, out var id))
            return Result<string>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid session ID.");

        var sessionToDelete = await _context.DoctorSessions.FirstOrDefaultAsync(ds => ds.Id == id);
        if (sessionToDelete == null)
            return Result<string>.Failure(ErrorCode.DRSSN_NotFound, "Session not found with given ID.");

        // Determine no bookings made for the session and safe to delete
        if (!await _context.SessionSlots.AnyAsync(ss => ss.SessionId == id) || await _context.SessionSlots.AllAsync(ss => ss.SessionId == id && ss.IsAvailable))
        {
            _context.DoctorSessions.Remove(sessionToDelete);
            await _context.SaveChangesAsync();
            return Result<string>.Success($"Doctor session with ID '{sessionId}' was successfully deleted.");
        }

        return Result<string>.Failure(ErrorCode.SYS_BadRequest, 
            "Existing appointments are linked to this session. If you need to discontinue it, please mark the session as inactive to prevent future appointment bookings.");
    }

    private static Result<T> ValidateOverlaps<T>(
        List<DoctorSession> sourceSessions,
        List<SessionOccurrence> targetOccurrences,
        TimeOnly targetStart,
        TimeOnly targetEnd)
    {
        // exact duplicates (same occurrences + same times)
        var exactDuplicate = sourceSessions
            .Where(ds => ds.Occurrences.OrderBy(x => x).SequenceEqual(targetOccurrences.OrderBy(x => x)))
            .Any(ds => ds.StartTime == targetStart && ds.EndTime == targetEnd);

        if (exactDuplicate)
            return Result<T>.Failure(ErrorCode.SYS_DuplicateEntry,
                "Another session already exists with the same schedule and occurrences.");

        // sessions with overlapping occurrence days and overlapping times
        var conflictingSessions = sourceSessions
            .Where(
                ds => ds.Occurrences.Contains(SessionOccurrence.AllDays) ||
                (ds.Occurrences.Contains(SessionOccurrence.Weekdays) && ExpandedWeekDays().Intersect(targetOccurrences).Any()) ||
                (ds.Occurrences.Contains(SessionOccurrence.Weekend) && ExpandedWeekendDays().Intersect(targetOccurrences).Any()) ||
                ds.Occurrences.Intersect(targetOccurrences).Any())
            .Where(ds => IsOverlapping(ds.StartTime, ds.EndTime, targetStart, targetEnd));

        if (conflictingSessions.Any())
            return Result<T>.Failure(ErrorCode.SYS_BadRequest,
                "The requested time slot conflicts with an existing session on overlapping days.");

        return Result<T>.Success(default);
    }

    private static bool IsOverlapping(TimeOnly start1, TimeOnly end1, TimeOnly start2, TimeOnly end2)
    {
        return start1 < end2 && start2 < end1;
    }

    private static List<SessionOccurrence> ExpandedWeekDays()
    {
        return [ 
            SessionOccurrence.Monday,
            SessionOccurrence.Tuesday,
            SessionOccurrence.Wednesday,
            SessionOccurrence.Thursday,
            SessionOccurrence.Friday,
        ];
    }

    private static List<SessionOccurrence> ExpandedWeekendDays()
    {
        return [
            SessionOccurrence.Saturday,
            SessionOccurrence.Sunday,
        ];
    }
}