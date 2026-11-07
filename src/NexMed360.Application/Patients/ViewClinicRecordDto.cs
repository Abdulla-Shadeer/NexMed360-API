namespace NexMed360Api.Dtos.ClinicRecord;

public class ViewClinicRecordDto
{
    public string Id { get; set; }
    public string PatientName { get; set; }
    public string DoctorName { get; set; }
    public string HospitalName { get; set; }
    public DateTime VisitDate { get; set; }
    public string Diagnosis { get; set; }
    public string? DoctorNotes { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<ViewPrescriptionDto>? Prescriptions { get; set; }
}
