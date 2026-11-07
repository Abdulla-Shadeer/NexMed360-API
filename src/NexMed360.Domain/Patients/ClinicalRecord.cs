namespace NexMed360Api.Entities;

public class ClinicalRecord: Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid? EncounterId { get; set; }
    public bool IsPatientVisible { get; set; } = true;

    public Guid PatientId { get; set; }

    public Guid PractitionerRoleId { get; set; }

    public DateTime VisitDate { get; set; }
    public string Diagnosis { get; set; }
    public string? PractitionerNotes { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<Prescription>? Prescriptions { get; set; } = [];

    public static ClinicalRecord Create(Guid patientId, Guid practitionerRoleId, string diagnosis, string? notes,
        DateTime? followUpDate, List<Prescription> prescriptions) => new()
    {
        PatientId = patientId, PractitionerRoleId = practitionerRoleId, Diagnosis = diagnosis, PractitionerNotes = notes,
        VisitDate = DateTime.UtcNow, FollowUpDate = followUpDate, Prescriptions = prescriptions
    };
}
