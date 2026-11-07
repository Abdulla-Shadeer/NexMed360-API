namespace NexMed360Api.Entities
{
    public class Patient: Entity
    {
        public DateOnly BirthDate { get; set; }
        public string Gender { get; set; }
        public string UserId { get; set; }

        public List<VisitHistory> VisitHistories { get; set; } = new List<VisitHistory>();

    }
}
