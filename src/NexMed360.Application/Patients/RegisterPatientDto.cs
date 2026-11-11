using System.ComponentModel.DataAnnotations;
using NexMed360Api.Dtos.User;

namespace NexMed360Api.Dtos.Patient;

public class RegisterPatientDto
{
    [Required] public RegisterUserDto PersonalInformation { get; set; } = null!;
    [Required] public string ConfirmPassword { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    [Required] public string Gender { get; set; } = "";
}
