namespace NexMed360Api.Entities
{
    public class Receptionist: Entity
    {
        public string Location { get; set; }
        public string Department { get; set; }

        public Guid HospitalId { get; set; }

        public string UserId { get; set; }

    }
}
