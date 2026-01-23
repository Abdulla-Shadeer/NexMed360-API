namespace NexMed360Api.Dtos.doctorSession;

public class ViewSessionSlotDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateOnly Date {  get; set; }
    public bool IsAvailable { get; set; }
    public bool BookingAllowed { get; set; }
    public int SlotNumber { get; set; }
}
