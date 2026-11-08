using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.OrganizationMember
{
    public class CreateOrganizationManagerDto
    {
        [Required]
        public RegisterUserDto PersonalInformation { get; set; }

        public List<string> Permissions { get; set; } = [];

        [Required]
        public string OrganizationId { get; set; }
    }
}
