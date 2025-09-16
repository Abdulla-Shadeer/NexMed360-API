namespace FinalProjectApi.Entities;

public class ClinicRecord: Entity
{
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; }

    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; }

    public DateTime VisitDate { get; set; }
    public string Diagnosis { get; set; }
    public string? DoctorNotes { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<Prescription>? Prescriptions { get; set; } = [];
}
