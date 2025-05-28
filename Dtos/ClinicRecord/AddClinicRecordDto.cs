using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.ClinicRecord;

public class AddClinicRecordDto
{
    [Required]
    public string PatientId { get; set; }

    [Required]
    public string DoctorId { get; set; }

    [Required]
    public DateTime VisitDate { get; set; }
    [Required]
    public string Diagnosis { get; set; }
    public string DoctorNotes { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<PrescriptionDto> Prescriptions { get; set; }
}
