namespace NexMed360Api.Dtos.Search;

public class SearchPractitionerRoleDto
{
    public bool ForBooking { get; set; }
    public DateOnly? Date { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Specialty { get; set; }
}
