namespace FinalProjectApi.Dtos.ClinicRecord;

public class ViewPrescriptionDto
{
    public string Id { get; set; }
    public string ClinicRecordId { get; set; }
    public DateTime IssuedOn { get; set; }
    public string IssuedByDoctor { get; set; }
    public string IssuedAtHospital { get; set; }
    public string MedicineName { get; set; }
    public string Dosage { get; set; }
    public int TimesPerDay { get; set; }
    public bool IsLongTermUse { get; set; }
    public string? DurationDays { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
