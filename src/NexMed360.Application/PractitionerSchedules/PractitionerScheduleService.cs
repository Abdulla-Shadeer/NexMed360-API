using NexMed360.Application.Shared;
using NexMed360.Domain.Scheduling;
using NexMed360.API.Entities.Enums;
using NexMed360.API.Dtos.practitionerSchedule;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.practitionerSchedule;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class PractitionerScheduleService(IPractitionerScheduleRepository repository, PortalViews views) : IPractitionerScheduleService
{
    private static bool Available(DateOnly date, PractitionerSchedule row) => new SessionSchedule(row.StartTime, row.EndTime, row.Occurrences).IsDateAvailable(date);
    public async Task<Result<ViewPractitionerScheduleDto>> Create(CreatePractitionerScheduleDto dto)
    {
        if (!Guid.TryParse(dto.PractitionerRoleId, out var practitionerRoleId)) return Result<ViewPractitionerScheduleDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Practitioner ID");
        if (!Guid.TryParse(dto.OrganizationId, out var organizationId)) return Result<ViewPractitionerScheduleDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Organization ID");
        if (!await repository.PractitionerRoleExists(practitionerRoleId, organizationId)) return Result<ViewPractitionerScheduleDto>.Failure(ErrorCode.APPT_PractitionerRoleNotFound);
        if (!await repository.OrganizationExists(organizationId)) return Result<ViewPractitionerScheduleDto>.Failure(ErrorCode.HOS_NotFound);
        if (dto.StartTime > dto.EndTime) return Result<ViewPractitionerScheduleDto>.Failure(ErrorCode.SYS_BadRequest, "Start time cannot be after End time!");
        var validation = ValidateOverlaps<ViewPractitionerScheduleDto>(await repository.GetPeers(practitionerRoleId, organizationId), dto.Occurrences, dto.StartTime, dto.EndTime);
        if (!validation.IsSuccess) return validation;
        var row = await repository.Add(new PractitionerSchedule
        {
            PractitionerRoleId = practitionerRoleId,
            OrganizationId = organizationId,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            SlotDurationMinutes = dto.SlotDurationMinutes ?? 30,
            Occurrences = dto.Occurrences
        });
        return Result<ViewPractitionerScheduleDto>.Success(await views.Session(row));
    }
    public async Task<Result<List<ViewPractitionerScheduleDto>>> Get(FilterPractitionerScheduleDto filter)
    {
        if (filter.PractitionerRoleId != null && !Guid.TryParse(filter.PractitionerRoleId, out _)) return Result<List<ViewPractitionerScheduleDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Practitioner ID");
        if (filter.OrganizationId != null && !Guid.TryParse(filter.OrganizationId, out _)) return Result<List<ViewPractitionerScheduleDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Organization ID");
        var rows = await repository.Get(new SessionFilter(filter.PractitionerRoleId == null ? null : Guid.Parse(filter.PractitionerRoleId), filter.OrganizationId == null ? null : Guid.Parse(filter.OrganizationId), filter.Date, filter.SessionStatus));
        return Result<List<ViewPractitionerScheduleDto>>.Success(await views.Sessions(rows));
    }
    public async Task<Result<List<ViewSessionSlotDto>>> GetSlots(GetSessionSlotsDto dto)
    {
        var session = await repository.Find(dto.SessionId);
        if (session == null) return Result<List<ViewSessionSlotDto>>.Failure(ErrorCode.SYS_BadRequest, "No sessions found with given ID");
        var slots = await repository.Slots(dto.SessionId, dto.Date);
        if (slots.Count > 0) return Result<List<ViewSessionSlotDto>>.Success(slots.Select(PortalViews.Slot).ToList());
        if (!Available(dto.Date, session)) return Result<List<ViewSessionSlotDto>>.Failure(ErrorCode.SYS_BadRequest, "Session not available on the requested date. Please choose a different date");
        await repository.AddSlots(session.GenerateSlots(dto.Date));
        return Result<List<ViewSessionSlotDto>>.Success((await repository.Slots(dto.SessionId, dto.Date)).Select(PortalViews.Slot).ToList());
    }
    public async Task<Result<ViewSessionHistoryDto>> Start(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var id)) return Result<ViewSessionHistoryDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Session ID");
        if (await repository.Find(id) == null) return Result<ViewSessionHistoryDto>.Failure(ErrorCode.DRSSN_NotFound);
        await repository.AddHistory(new SessionHistory { OrganizationId = (await repository.Find(id))!.OrganizationId, PractitionerScheduleId = id });
        return Result<ViewSessionHistoryDto>.Success(await views.History(await repository.History(id, DateOnly.FromDateTime(DateTime.Today))));
    }
    public async Task<Result<ViewSessionHistoryDto>> UpdateHistory(UpdateSessionHistoryDto dto)
    {
        if (!Guid.TryParse(dto.SessionId, out var id)) return Result<ViewSessionHistoryDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Session ID");
        if (dto.SessionStatus == SessionStatus.Canceled)
        {
            await repository.AddHistory(new SessionHistory { OrganizationId = (await repository.Find(id))!.OrganizationId, PractitionerScheduleId = id, SessionStatus = SessionStatus.Canceled });
            return Result<ViewSessionHistoryDto>.Success(await views.History(await repository.History(id, DateOnly.FromDateTime(DateTime.Today))));
        }
        var history = await repository.History(id, dto.Date);
        if (history == null) return Result<ViewSessionHistoryDto>.Failure(ErrorCode.DRSSN_HSTRY_NotFound);
        if (dto.SessionStatus != null) history.ChangeStatus((SessionStatus)dto.SessionStatus, DateTime.Now);
        if (dto.LastKnownAppointmentId != null)
        {
            if (!Guid.TryParse(dto.LastKnownAppointmentId, out var appointmentId)) return Result<ViewSessionHistoryDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Appointment ID Format");
            history.RecordEncounter(appointmentId);
        }
        await repository.SaveChanges(); return Result<ViewSessionHistoryDto>.Success(await views.History(history));
    }
    public async Task<Result<List<ViewSessionHistoryDto>>> GetHistory(FilterSessionHistoryDto filter)
    {
        if (filter.SessionId != null && !Guid.TryParse(filter.SessionId, out _)) return Result<List<ViewSessionHistoryDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Session ID");
        if (filter.PractitionerRoleId != null && !Guid.TryParse(filter.PractitionerRoleId, out _)) return Result<List<ViewSessionHistoryDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Practitioner ID");
        if (filter.OrganizationId != null && !Guid.TryParse(filter.OrganizationId, out _)) return Result<List<ViewSessionHistoryDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Organization ID");
        var rows = await repository.Histories(new SessionHistoryFilter(filter.SessionId == null ? null : Guid.Parse(filter.SessionId), filter.PractitionerRoleId == null ? null : Guid.Parse(filter.PractitionerRoleId),
            filter.OrganizationId == null ? null : Guid.Parse(filter.OrganizationId), filter.Status, filter.FromDate, filter.ToDate));
        return Result<List<ViewSessionHistoryDto>>.Success(await views.Histories(rows));
    }
    public async Task<Result<ViewCurrentOrNextSessionDto>> GetCurrentOrUpcoming(FilterSessionsForCurrentDayDto filter)
    {
        if (!Guid.TryParse(filter.PractitionerRoleId, out var practitionerRoleId)) return Result<ViewCurrentOrNextSessionDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Practitioner ID");
        if (!Guid.TryParse(filter.OrganizationId, out var organizationId)) return Result<ViewCurrentOrNextSessionDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Organization ID");
        if (!DateOnly.TryParse(filter.Date, out var date)) return Result<ViewCurrentOrNextSessionDto>.Failure(ErrorCode.SYS_BadRequest, "Invalid Date format");
        var current = await repository.Current(practitionerRoleId, organizationId, date);
        if (current != null) return Result<ViewCurrentOrNextSessionDto>.Success(PortalViews.Current(current));
        var now = new TimeOnly(DateTime.Now.TimeOfDay.Ticks);
        var upcoming = repository.GetForPractitionerRoleOrganization(practitionerRoleId, organizationId).Where(row => Available(date, row) && row.EndTime > now).ToList();
        var closed = await repository.ClosedSessionIds(date); upcoming.RemoveAll(row => closed.Contains(row.Id));
        return Result<ViewCurrentOrNextSessionDto>.Success(PortalViews.Current(upcoming.OrderBy(row => row.StartTime).FirstOrDefault()));
    }
    public Result<bool> HasAnySessionToday(FilterSessionsForCurrentDayDto filter)
    {
        if (!Guid.TryParse(filter.PractitionerRoleId, out var practitionerRoleId)) return Result<bool>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Practitioner ID");
        if (!Guid.TryParse(filter.OrganizationId, out var organizationId)) return Result<bool>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Organization ID");
        if (!DateOnly.TryParse(filter.Date, out var date)) return Result<bool>.Failure(ErrorCode.SYS_BadRequest, "Invalid Date format");
        return Result<bool>.Success(repository.GetForPractitionerRoleOrganization(practitionerRoleId, organizationId).Any(row => Available(date, row)));
    }
    public async Task<Result<List<ViewSessionHistoryDto>>> GetOngoing(string userId)
        => Result<List<ViewSessionHistoryDto>>.Success(await views.Histories(await repository.Ongoing(await repository.OrganizationMemberOrganization(userId), DateOnly.FromDateTime(DateTime.Today))));
    public async Task<Result<ViewPractitionerScheduleDto>> Update(string sessionId, UpdatePractitionerScheduleDto dto)
    {
        if (!Guid.TryParse(sessionId, out var id)) return Result<ViewPractitionerScheduleDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Session ID");
        if (dto.StartTime > dto.EndTime) return Result<ViewPractitionerScheduleDto>.Failure(ErrorCode.SYS_BadRequest, "Start time cannot be after End time!");
        var row = await repository.Find(id); if (row == null) return Result<ViewPractitionerScheduleDto>.Failure(ErrorCode.DRSSN_NotFound);
        var validation = ValidateOverlaps<ViewPractitionerScheduleDto>(await repository.GetPeers(row.PractitionerRoleId, row.OrganizationId, id), dto.Occurrences, dto.StartTime, dto.EndTime);
        if (!validation.IsSuccess) return validation;
        row.StartTime = dto.StartTime; row.EndTime = dto.EndTime; row.SlotDurationMinutes = dto.SlotDurationMinutes; row.Occurrences = dto.Occurrences; row.IsActive = dto.IsActive;
        await repository.SaveChanges(); return Result<ViewPractitionerScheduleDto>.Success(await views.Session(row));
    }
    public async Task<Result<string>> Delete(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var id)) return Result<string>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid session ID.");
        var row = await repository.Find(id); if (row == null) return Result<string>.Failure(ErrorCode.DRSSN_NotFound, "Session not found with given ID.");
        if (await repository.CanDelete(id)) { repository.Remove(row); await repository.SaveChanges(); return Result<string>.Success($"Practitioner session with ID '{sessionId}' was successfully deleted."); }
        return Result<string>.Failure(ErrorCode.SYS_BadRequest, "Existing appointments are linked to this session. If you need to discontinue it, please mark the session as inactive to prevent future appointment bookings.");
    }
    private static Result<T> ValidateOverlaps<T>(
        List<PractitionerSchedule> sourceSessions,
        List<SessionOccurrence> targetOccurrences,
        TimeOnly targetStart,
        TimeOnly targetEnd)
    {
        var target = new SessionSchedule(targetStart, targetEnd, targetOccurrences);
        var schedules = sourceSessions.Select(session => new SessionSchedule(session.StartTime, session.EndTime, session.Occurrences)).ToList();
        if (schedules.Any(schedule => schedule.IsExactDuplicate(target)))
            return Result<T>.Failure(ErrorCode.SYS_DuplicateEntry,
                "Another session already exists with the same schedule and occurrences.");
        if (schedules.Any(schedule => schedule.Overlaps(target)))
            return Result<T>.Failure(ErrorCode.SYS_BadRequest,
                "The requested time slot conflicts with an existing session on overlapping days.");
        return Result<T>.Success(default);
    }
}
