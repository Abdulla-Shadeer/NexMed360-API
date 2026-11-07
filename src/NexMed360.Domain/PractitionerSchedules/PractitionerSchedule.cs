using NexMed360.API.Entities.Enums;

namespace NexMed360Api.Entities;
public class PractitionerSchedule : Entity, IOrganizationOwned
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid PractitionerRoleId { get; set; }

    public Guid OrganizationId { get; set; }

    public int SlotDurationMinutes { get; set; } = 30;
    public List<SessionSlot> SessionSlots { get; set; } = [];
    public List<SessionOccurrence> Occurrences { get; set; } = [];
    public List<SessionHistory> History { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public int TotalSlots => new NexMed360.Domain.Scheduling.SessionSchedule(StartTime, EndTime, Occurrences).SlotCount(SlotDurationMinutes);

    public List<SessionSlot> GenerateSlots(DateOnly date)
    {
        var slots = new List<SessionSlot>();
        for (var index = 0; index < TotalSlots; index++)
        {
            var start = StartTime.AddMinutes(index * SlotDurationMinutes);
            slots.Add(new SessionSlot { OrganizationId = OrganizationId, SessionId = Id, Date = date, StartTime = start,
                EndTime = start.AddMinutes(SlotDurationMinutes), IsAvailable = true, SlotNumber = index + 1 });
        }
        return slots;
    }

}

public class SessionSlot : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public DateOnly Date { get; set; }
    public Guid SessionId { get; set; }
    public PractitionerSchedule Session { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public bool BookingAllowed { get; set; } = true;
    public int SlotNumber { get; set; }
}
