using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Organization
{
    public class CreateOrganizationDto
    {
        public string Type { get; set; } = "Healthcare provider";
    [Required]
    public string Name { get; set; }

        public string Street { get; set; }
        public string City { get; set; }
        public string District { get; set; }

        [Required]
        public string Phone { get; set; }
        public string? Grade { get; set; }
    }
}
