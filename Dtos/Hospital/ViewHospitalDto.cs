using NexMed360Api.Dtos.User;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Hospital;

public class ViewHospitalDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string Phone { get; set; }
    public string? Grade { get; set; }
    public List<ViewUserDto>? Admins { get; set; }
}
