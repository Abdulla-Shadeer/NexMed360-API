using System.ComponentModel.DataAnnotations;

namespace NexMed360.Application.Patients;

public sealed class CreateRecordSharingRequestDto
{
    public Guid PatientId { get; set; }
    public Guid PractitionerRoleId { get; set; }
    [Required, MaxLength(1000)]
    public string Purpose { get; set; } = "";
    public DateOnly? RecordFromDate { get; set; }
    public DateOnly? RecordToDate { get; set; }
    public DateOnly? AccessExpiresOn { get; set; }
}

public sealed record DecideRecordSharingRequestDto(bool Approve);

public sealed class RecordSharingRequestDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = "";
    public Guid ReceivingOrganizationId { get; set; }
    public string OrganizationName { get; set; } = "";
    public string OrganizationContact { get; set; } = "";
    public string PractitionerName { get; set; } = "";
    public string PractitionerRole { get; set; } = "";
    public string PractitionerEmail { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateOnly? RecordFromDate { get; set; }
    public DateOnly? RecordToDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = "";
    public DateTime? RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
