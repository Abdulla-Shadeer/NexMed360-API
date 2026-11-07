namespace NexMed360Api.Entities;

public class ClinicRecord: Entity
{
    public Guid PatientId { get; set; }

    public Guid DoctorId { get; set; }

    public DateTime VisitDate { get; set; }
    public string Diagnosis { get; set; }
    public string? DoctorNotes { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<Prescription>? Prescriptions { get; set; } = [];

    public static ClinicRecord Create(Guid patientId, Guid doctorId, string diagnosis, string? notes,
        DateTime? followUpDate, List<Prescription> prescriptions) => new()
    {
        PatientId = patientId, DoctorId = doctorId, Diagnosis = diagnosis, DoctorNotes = notes,
        VisitDate = DateTime.UtcNow, FollowUpDate = followUpDate, Prescriptions = prescriptions
    };
}
