namespace NexMed360Api.Entities;

public class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime? CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; } = DateTime.UtcNow;
}

public interface IOrganizationOwned
{
    Guid OrganizationId { get; set; }
}
