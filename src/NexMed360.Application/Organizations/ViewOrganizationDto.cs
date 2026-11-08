using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Organization;

public class ViewOrganizationDto
{
    public string Id { get; set; }
    public string Type { get; set; } = "Healthcare provider";
    public string Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string Phone { get; set; }
    public string? Grade { get; set; }
}
