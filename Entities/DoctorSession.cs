using NexMed360.API.Entities.Enums;
using NexMed360Api.Common;

namespace NexMed360Api.Entities;
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
    public List<SessionHistory> History { get; set; } = [];
    public bool IsActive { get; set; } = true;
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
