using NexMed360Api.Dtos.User;

namespace NexMed360Api.Dtos.Patient;

public class ViewPatientDto
{
    public string PersonId { get; set; } = "";
        public string Id { get; set; }
    public string UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public DateOnly BirthDate { get; set; }
    public string Gender { get; set; }
}
