namespace NexMed360Api.Dtos.ClinicalRecord;

public class ViewClinicalRecordDto
{
    public string Id { get; set; }
    public string PatientName { get; set; }
    public string PractitionerName { get; set; }
    public string OrganizationName { get; set; }
    public Guid OrganizationId { get; set; }
    public bool IsShared { get; set; }
    public bool IsPatientVisible { get; set; }
    public DateTime VisitDate { get; set; }
    public string Diagnosis { get; set; }
    public string? PractitionerNotes { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<ViewPrescriptionDto>? Prescriptions { get; set; }
}
