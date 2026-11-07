using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Entities
{
    public class VisitHistory: Entity
    {
        public DateTime VisitDate { get; set; }
        public VisitReason Reason { get; set; }
        public Guid HospitalId { get; set; }

        public string UserId { get; set; }

    }
}
