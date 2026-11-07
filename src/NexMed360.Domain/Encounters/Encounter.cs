namespace NexMed360Api.Entities;

public enum EncounterStatus { InProgress, Completed, Cancelled }

public class Encounter : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? LocationId { get; set; }
    public EncounterStatus Status { get; set; } = EncounterStatus.InProgress;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public List<EncounterParticipant> Participants { get; set; } = [];
}

public class EncounterParticipant : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public Guid EncounterId { get; set; }
    public Guid PractitionerRoleId { get; set; }
}
