namespace FinalProjectApi.Dtos.ClinicRecord;

public class ViewPrescriptionDto
{
    public string Id { get; set; }
    public string ClinicRecordId { get; set; }
    public string MedicineName { get; set; }
    public string Dosage { get; set; }
    public string Frequency { get; set; }
    public string DurationDays { get; set; }
    public string? Notes { get; set; }
}
