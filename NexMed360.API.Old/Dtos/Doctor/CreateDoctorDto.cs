using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Doctor
{
    public class CreateDoctorDto
    {
        [Required]
        public RegisterUserDto PersonalInformation { get; set; }
        [Required]
        public string Speciality { get; set; }

        [Required]
        public string HospitalId { get; set; }
    }
}
