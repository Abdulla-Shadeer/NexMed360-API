namespace NexMed360Api.Entities;

public class ExternalPatientLink : Entity
{
    public Guid LocalPatientId { get; set; }
    public string PartnerId { get; set; } = "";
    public string ExternalPatientId { get; set; } = "";
    public string ConfirmedByUserId { get; set; } = "";
    public DateTime ConfirmedAt { get; set; } = DateTime.UtcNow;
}
