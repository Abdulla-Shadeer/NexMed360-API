using NexMed360.API.Entities.Enums;

namespace NexMed360.Domain.Scheduling;

public sealed record SessionSchedule(TimeOnly Start, TimeOnly End, IReadOnlyCollection<SessionOccurrence> Occurrences)
{
    public int SlotCount(int durationMinutes) => (int)Math.Floor((End - Start).TotalMinutes / durationMinutes);

    public bool IsExactDuplicate(SessionSchedule target)
        => Occurrences.OrderBy(value => value).SequenceEqual(target.Occurrences.OrderBy(value => value))
            && Start == target.Start && End == target.End;

    // Preserve the existing occurrence comparison, including its direction.
    public bool Overlaps(SessionSchedule target)
        => (Occurrences.Contains(SessionOccurrence.AllDays)
            || (Occurrences.Contains(SessionOccurrence.Weekdays) && Weekdays.Intersect(target.Occurrences).Any())
            || (Occurrences.Contains(SessionOccurrence.Weekend) && Weekend.Intersect(target.Occurrences).Any())
            || Occurrences.Intersect(target.Occurrences).Any())
            && Start < target.End && target.Start < End;

    public bool IsDateAvailable(DateOnly date)
    {
        if (Occurrences.Contains(SessionOccurrence.AllDays)) return true;
        var day = date.DayOfWeek;
        if (Occurrences.Contains(SessionOccurrence.Weekdays) && day >= DayOfWeek.Monday && day <= DayOfWeek.Friday) return true;
        if (Occurrences.Contains(SessionOccurrence.Weekend) && (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday)) return true;
        return Enum.TryParse<SessionOccurrence>(day.ToString(), true, out var occurrence) && Occurrences.Contains(occurrence);
    }

    private static readonly SessionOccurrence[] Weekdays =
        [SessionOccurrence.Monday, SessionOccurrence.Tuesday, SessionOccurrence.Wednesday, SessionOccurrence.Thursday, SessionOccurrence.Friday];
    private static readonly SessionOccurrence[] Weekend = [SessionOccurrence.Saturday, SessionOccurrence.Sunday];
}
