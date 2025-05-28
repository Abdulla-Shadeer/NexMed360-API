namespace FinalProjectApi.Dtos.ClinicRecord;

public class ClinicRecordDto
{
    public string PatientId { get; set; }
    public string PatientName { get; set; }
    public string DoctorId { get; set; }
    public string DoctorName { get; set; }
    public DateTime VisitDate { get; set; }
    public string Diagnosis { get; set; }
    public string? DoctorNotes { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<PrescriptionDto>? Prescriptions { get; set; }
}
