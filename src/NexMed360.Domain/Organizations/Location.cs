namespace NexMed360Api.Entities;

public enum LocationType { Site, Department, Ward, Room, Bed, Other }

public class Location : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public LocationType Type { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
