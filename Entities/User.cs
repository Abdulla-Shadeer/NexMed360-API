using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Entities
{
    public class User: IdentityUser
    {
        [Required]
        [StringLength(250)]
        public string? FirstName { get; set; }

        [Required]
        [StringLength(250)]
        public string? LastName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }   
}
