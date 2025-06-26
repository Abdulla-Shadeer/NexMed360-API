using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.ClinicRecord;

public class AddPrescriptionDto
{
    [Required]
    public string MedicineName { get; set; }
    [Required]
    public string Dosage { get; set; }
    [Required]
    public string Frequency { get; set; }
    public string? DurationDays { get; set; }
    public string? Notes { get; set; }
}
