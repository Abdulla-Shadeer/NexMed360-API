namespace NexMed360Api.Entities;

public class Practitioner : Entity
{
    public Guid PersonId { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Qualifications { get; set; }
}
