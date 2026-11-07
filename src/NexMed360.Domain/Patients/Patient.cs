namespace NexMed360Api.Entities;

public class Patient : Entity
{
    public Guid PersonId { get; set; }
    public DateOnly BirthDate { get; set; }
    public string Gender { get; set; } = "";
    public List<VisitHistory> VisitHistories { get; set; } = [];
}
