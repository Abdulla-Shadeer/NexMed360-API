namespace FinalProjectApi.Dtos.Appointment;

public class ViewSessionSlotDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateOnly Date {  get; set; }
    public bool IsAvailable { get; set; }
}
