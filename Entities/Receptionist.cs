namespace FinalProjectApi.Entities
{
    public class Receptionist: Entity
    {
        public string Location { get; set; }
        public string Department { get; set; }

        public Guid HospitalId { get; set; }
        public Hospital Hospital { get; set; }

        public string UserId { get; set; }
        public User User { get; set; }
    }
}
