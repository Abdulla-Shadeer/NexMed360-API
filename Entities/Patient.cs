namespace NexMed360Api.Entities
{
    public class Patient: Entity
    {
        public DateOnly BirthDate { get; set; }
        public string Gender { get; set; }
        public string UserId { get; set; }
        public User User { get; set; }
        public List<VisitHistory> VisitHistories { get; set; } = new List<VisitHistory>();
        public List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public List<ClinicRecord> ClinicRecords { get; set; } = new List<ClinicRecord>();
    }
}
