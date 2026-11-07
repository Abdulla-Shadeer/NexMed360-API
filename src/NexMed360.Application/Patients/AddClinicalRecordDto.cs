using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.ClinicalRecord;

public class AddClinicalRecordDto
{
    [Required]
    public Guid PractitionerRoleId { get; set; }

    public string PatientId { get; set; }

    [Required]
    public string Diagnosis { get; set; }
    public string? PractitionerNotes { get; set; }
    public DateTime VisitDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<AddPrescriptionDto>? Prescriptions { get; set; } = new List<AddPrescriptionDto>();
}
