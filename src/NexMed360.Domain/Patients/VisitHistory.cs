using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Entities
{
    public class VisitHistory: Entity, IOrganizationOwned
    {
        public DateTime VisitDate { get; set; }
        public VisitReason Reason { get; set; }
        public Guid OrganizationId { get; set; }

        public Guid PatientId { get; set; }

    }
}
