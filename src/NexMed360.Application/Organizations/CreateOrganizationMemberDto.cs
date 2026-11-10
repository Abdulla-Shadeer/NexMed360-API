using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.OrganizationMember
{
    public class CreateOrganizationMemberDto
    {
        [Required]
        public RegisterUserDto PersonalInformation { get; set; }

        public Guid? LocationId { get; set; }

        [Required]
        public string OrganizationId { get; set; }
    }
}
