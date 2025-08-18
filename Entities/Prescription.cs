namespace FinalProjectApi.Entities;

public class Prescription: Entity
{
    public string MedicineName { get; set; }
    public string Dosage { get; set; }
    public int TimesPerDay { get; set; }
    public int DurationDays { get; set; }
    public string? Notes { get; set; }
    public Guid ClinicRecordId { get; set; }
    public ClinicRecord ClinicRecord { get; set; }
    public bool IsActive { get; set; } = true;
}
