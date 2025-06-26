using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.User
{
    public class RegisterUserDto
    {
        [Required]
        [StringLength(250)]
        public string? FirstName { get; set; }

        [Required]
        [StringLength(250)]
        public string? LastName { get; set; }

        [Required]
        public string Email { get; set; }

        public string PhoneNumber { get; set; }
        
        [Required]
        public string Password { get; set; }
    }
}
