using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.ClinicRecord;

public class AddClinicRecordDto
{
    [Required]
    public string PatientId { get; set; }

    [Required]
    public string Diagnosis { get; set; }
    public string? DoctorNotes { get; set; }
    public DateTime VisitDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<AddPrescriptionDto>? Prescriptions { get; set; } = new List<AddPrescriptionDto>();
}
