using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.ClinicalRecord;

public class AddPrescriptionDto
{
    [Required]
    public string MedicineName { get; set; }
    [Required]
    public string Dosage { get; set; }
    [Required]
    public int TimesPerDay { get; set; }
    [Required]
    public bool? IsLongTermUse { get; set; }
    public string? Duration { get; set; }
    public string? Notes { get; set; }
}
