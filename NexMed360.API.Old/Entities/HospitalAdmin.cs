namespace NexMed360Api.Entities
{
    public class HospitalAdmin: Entity
    {
        public Guid HospitalId { get; set; }
        public Hospital Hospital { get; set; }

        public string UserId { get; set; }
        public User User { get; set; }
    }
}
