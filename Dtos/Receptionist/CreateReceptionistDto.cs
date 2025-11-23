using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Receptionist
{
    public class CreateReceptionistDto
    {
        [Required]
        public RegisterUserDto PersonalInformation { get; set; }

        public string Location { get; set; }
        public string Department { get; set; }

        [Required]
        public string HospitalId { get; set; }
    }
}
