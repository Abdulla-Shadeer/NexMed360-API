using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Dtos.practitionerSchedule;

namespace NexMed360Api.Services;

internal static class ScheduleMapping
{
    public static ViewSessionSlotDto Slot(SessionSlot row) => new()
    {
        Id = row.Id,
        SessionId = row.SessionId,
        StartTime = row.StartTime,
        EndTime = row.EndTime,
        Date = row.Date,
        IsAvailable = row.IsAvailable,
        BookingAllowed = row.BookingAllowed,
        SlotNumber = row.SlotNumber
    };
    public static ViewCurrentOrNextSessionDto? Current(PractitionerSchedule? row) => row == null ? null : new()
    {
        IsCurrent = row.History.Any(),
        SessionId = row.Id.ToString(),
        StartTime = row.StartTime.ToString(),
        EndTime = row.EndTime.ToString(),
        StartedTime = row.History.FirstOrDefault()?.StartTime.ToString(),
        LastKnownAppointmentId = row.History.FirstOrDefault()?.LastKnownAppointmentId?.ToString(),
        EncountersCount = row.History.FirstOrDefault()?.EncountersCount ?? 0
    };
    public static ViewCurrentOrNextSessionDto Current(SessionHistory row) => new()
    {
        IsCurrent = row.SessionStatus == SessionStatus.Started,
        SessionId = row.PractitionerScheduleId.ToString(),
        StartTime = row.PractitionerSchedule.StartTime.ToString(),
        EndTime = row.PractitionerSchedule.EndTime.ToString(),
        StartedTime = row.StartTime.ToString(),
        LastKnownAppointmentId = row.LastKnownAppointmentId?.ToString(),
        EncountersCount = row.EncountersCount
    };
}
