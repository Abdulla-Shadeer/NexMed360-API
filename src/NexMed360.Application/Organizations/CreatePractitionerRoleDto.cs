using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.PractitionerRole
{
    public class CreatePractitionerRoleDto
    {
        [Required]
        public RegisterUserDto PersonalInformation { get; set; }
        [Required]
        public string RoleName { get; set; } = "Practitioner";
        public string? Specialty { get; set; }
        public Guid? LocationId { get; set; }
        public List<Guid> AccessRoleIds { get; set; } = [];

        [Required]
        public string OrganizationId { get; set; }
    }
}
