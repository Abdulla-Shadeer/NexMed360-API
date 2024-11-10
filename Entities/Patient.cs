namespace FinalProjectApi.Entities
{
    public class Patient: Entity
    {
        public DateOnly BirthDate { get; set; }
        public string Gender { get; set; }

        public string UserId { get; set; }
        public User User { get; set; }
    }
}
