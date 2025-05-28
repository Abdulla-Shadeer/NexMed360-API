namespace FinalProjectApi.Dtos.ClinicRecord;

public class PrescriptionDto
{
    public string MedicineName { get; set; }
    public string Dosage { get; set; }
    public string Frequency { get; set; }
    public string DurationDays { get; set; }
    public string? Notes { get; set; }
}
