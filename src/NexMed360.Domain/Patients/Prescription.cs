namespace NexMed360Api.Entities;

public class Prescription: Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public string MedicineName { get; set; }
    public string Dosage { get; set; }
    public int TimesPerDay { get; set; }
    public bool IsLongTermUse { get; set; } = false;
    public string? Duration { get; set; }
    public string? Notes { get; set; }
    public Guid ClinicalRecordId { get; set; }
    public ClinicalRecord ClinicalRecord { get; set; }
    public bool IsActive { get; set; } = true;
}
