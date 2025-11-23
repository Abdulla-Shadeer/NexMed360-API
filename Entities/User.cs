using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Entities
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

        public HospitalAdmin HospitalAdmin { get; set; }
        public Receptionist Receptionist { get; set; }
        public Doctor Doctor { get; set; }
        public Patient Patient { get; set; }
    }   
}
