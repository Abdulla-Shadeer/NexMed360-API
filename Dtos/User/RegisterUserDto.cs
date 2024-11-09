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

        public string Phone { get; set; }

        [Required]
        public string Role { get; set; } // role name

        public DateTime? CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }
        
        [Required]
        public string password { get; set; }
    }
}
