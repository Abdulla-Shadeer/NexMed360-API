namespace FinalProjectApi.Entities
{
    public class Doctor: Entity
    {
        public string Speciality { get; set; }

        public Guid HospitalId { get; set; }
        public Hospital Hospital { get; set; }
        
        public string UserId { get; set; }
        public User User { get; set; }

        public List<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
