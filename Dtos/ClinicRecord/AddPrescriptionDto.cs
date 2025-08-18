using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.ClinicRecord;

public class AddPrescriptionDto
{
    [Required]
    public string MedicineName { get; set; }
    [Required]
    public string Dosage { get; set; }
    [Required]
    public int TimesPerDay { get; set; }
    [Required]
    public int DurationDays { get; set; }
    public string? Notes { get; set; }
}
