using FinalProjectApi.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.HospitalAdmin
{
    public class CreateHospitalAdminDto
    {
        [Required]
        public RegisterUserDto PersonalInformation { get; set; }

        [Required]
        public string HospitalId { get; set; }
    }
}
