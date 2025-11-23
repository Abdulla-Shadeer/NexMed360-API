using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.HospitalAdmin
{
    public class CreateHospitalAdminDto
    {
        [Required]
        public RegisterUserDto PersonalInformation { get; set; }

        [Required]
        public string HospitalId { get; set; }
    }
}
