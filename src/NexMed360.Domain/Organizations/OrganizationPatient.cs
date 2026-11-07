namespace NexMed360Api.Entities;

public class OrganizationPatient : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public string? PatientNumber { get; set; }
}
