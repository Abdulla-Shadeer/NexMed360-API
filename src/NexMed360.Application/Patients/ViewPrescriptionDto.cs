namespace NexMed360Api.Dtos.ClinicalRecord;

public class ViewPrescriptionDto
{
    public string Id { get; set; }
    public string ClinicalRecordId { get; set; }
    public DateTime IssuedOn { get; set; }
    public string IssuedByPractitioner { get; set; }
    public string IssuedAtOrganization { get; set; }
    public string MedicineName { get; set; }
    public string Dosage { get; set; }
    public int TimesPerDay { get; set; }
    public bool IsLongTermUse { get; set; }
    public string? Duration { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
