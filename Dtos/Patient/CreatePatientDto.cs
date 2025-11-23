using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Patient
{
    public class CreatePatientDto
    {
        [Required]
        public RegisterUserDto PersonalInformation { get; set; }

        [Required]
        public DateOnly BirthDate { get; set; }

        [Required]
        public string Gender { get; set; }
    }
}
