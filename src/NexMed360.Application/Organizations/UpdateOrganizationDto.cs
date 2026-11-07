using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Organization;

public class UpdateOrganizationDto
{
    [Required]
    public string Id { get; set; }
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Phone { get; set; }
    public string? Grade { get; set; }
}
