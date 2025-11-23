using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360Api.Common;

public static class AppointmentHelper
{
    public readonly static int DEFAULT_SLOT_DURATION_MINUTES = 30;
    public static bool IsTimeSlotAvailable
        (
        ApplicationDbContext dbContext,
        Guid personId,
        DateTime startTime,
        bool isDoctor
        )
    {
        var defaultDuration = TimeSpan.FromMinutes(30);
        var endTime = startTime.Add(defaultDuration);

        return !dbContext.Appointments
            .Where(ap => isDoctor ? ap.DoctorId == personId : ap.PatientId == personId)
            .Select(ap => new
            {
                ap.AppointmentDateTime
            })
            .AsEnumerable()
            .Any(ap => ap.AppointmentDateTime < endTime
                && ap.AppointmentDateTime.Add(defaultDuration) > startTime);
    }

    public static int GetTotalSlots(DoctorSession session)
    {
        return (int)Math.Floor((session.EndTime - session.StartTime).TotalMinutes / session.SlotDurationMinutes);
    }

    /// <summary>
    /// Slot generation should check the occurrence configuration to prevent creating slots on unavailable days
    /// </summary>
    public static Result<List<SessionSlot>> CheckAndGenerateSlots(DoctorSession session, DateOnly date)
    {
        if (!IsDateAvailable(date, session.Occurrences)) { 
            return Result<List<SessionSlot>>
                .Failure(ErrorCode.SYS_BadRequest, "Session not available on the requested date. Please choose a different date");
        }

        var slots = new List<SessionSlot>();
        for (int i = 0; i < GetTotalSlots(session); i++)
        {
            var slot = new SessionSlot
            {
                SessionId = session.Id,
                StartTime = session.StartTime.AddMinutes(i * session.SlotDurationMinutes)
            };
            slot.EndTime = slot.StartTime.AddMinutes(session.SlotDurationMinutes);
            slot.IsAvailable = true;
            slot.Date = date;
            slot.SlotNumber = i + 1;
            slots.Add(slot);
        }
        return Result<List<SessionSlot>>.Success(slots);
    } 

    public static bool IsDateAvailable(DateOnly date, List<SessionOccurrence> occurrences) {
        if (occurrences.Contains(SessionOccurrence.AllDays)) { 
            return true;
        }

        var requestedDay = date.DayOfWeek;

        if (occurrences.Contains(SessionOccurrence.Weekdays))
        {
            var requestedDayIsWeekDay = requestedDay >= DayOfWeek.Monday && requestedDay <= DayOfWeek.Friday;
            if (requestedDayIsWeekDay) { 
                return true;
            }
        }

        if (occurrences.Contains(SessionOccurrence.Weekend)) 
        {
            var requestedDayIsWeekend = requestedDay == DayOfWeek.Sunday || requestedDay == DayOfWeek.Saturday;
            if (requestedDayIsWeekend) { 
                return true ;
            }
        }

        if (Enum.TryParse(typeof(SessionOccurrence), requestedDay.ToString(), true, out var dayAsEnum)) {
            if (occurrences.Contains((SessionOccurrence)dayAsEnum)) { 
                return true;
            }
        }

        return false;
    }
}
