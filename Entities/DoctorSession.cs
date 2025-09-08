using FinalProjectApi.Common;
using System.Text.Json.Serialization;

namespace FinalProjectApi.Entities;

public class DoctorSession : Entity
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; }
    public Guid HospitalId { get; set; }
    public Hospital Hospital { get; set; }
    public int SlotDurationMinutes { get; set; } = AppointmentHelper.DEFAULT_SLOT_DURATION_MINUTES;
    public List<SessionSlot> SessionSlots { get; set; } = [];
    public List<SessionOccurrence> Occurrences { get; set; } = [];
}

public class SessionSlot : Entity
{ 
    public DateOnly Date { get; set; }
    public Guid SessionId { get; set; }
    public DoctorSession Session { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public bool BookingAllowed { get; set; } = true;
    public int SlotNumber { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionOccurrence
{
    Weekdays,
    Weekend,
    AllDays,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
}
