namespace NexMed360Api.Entities;

public enum RecordSharingStatus { Pending, Approved, Rejected, Revoked }

public class RecordSharingRequest : Entity
{
    public Guid PatientId { get; set; }
    public Guid? ReceivingOrganizationId { get; set; }
    public Guid? PractitionerRoleId { get; set; }
    public string? ReceivingPartnerId { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterRole { get; set; }
    public string? RequesterContact { get; set; }
    public string Purpose { get; set; } = "";
    public DateOnly? RecordFromDate { get; set; }
    public DateOnly? RecordToDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public RecordSharingStatus Status { get; private set; } = RecordSharingStatus.Pending;
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? DecisionByUserId { get; private set; }

    public bool IsApprovedAt(DateTime now) => Status == RecordSharingStatus.Approved && ApprovedAt.HasValue && ApprovedAt <= now
        && ExpiresAt > now && !RevokedAt.HasValue;

    public bool AllowsRecord(ClinicalRecord record, DateTime now) => IsApprovedAt(now)
        && record.PatientId == PatientId && record.IsPatientVisible && record.CreatedDate.HasValue
        && record.CreatedDate <= ApprovedAt && record.VisitDate <= ApprovedAt
        && (!RecordFromDate.HasValue || DateOnly.FromDateTime(record.VisitDate) >= RecordFromDate.Value)
        && (!RecordToDate.HasValue || DateOnly.FromDateTime(record.VisitDate) <= RecordToDate.Value);

    public bool AllowsPrescription(Prescription prescription, ClinicalRecord record, DateTime now) => AllowsRecord(record, now)
        && prescription.ClinicalRecordId == record.Id && prescription.OrganizationId == record.OrganizationId
        && prescription.CreatedDate.HasValue && prescription.CreatedDate <= ApprovedAt;

    public void Decide(bool approve, string userId, DateTime now)
    {
        if (Status != RecordSharingStatus.Pending || ExpiresAt <= now)
            throw new InvalidOperationException("This request is no longer waiting for a decision.");

        Status = approve ? RecordSharingStatus.Approved : RecordSharingStatus.Rejected;
        ApprovedAt = approve ? now : null;
        DecidedAt = UpdatedDate = now;
        DecisionByUserId = userId;
    }

    public void Revoke(string userId, DateTime now)
    {
        if (Status != RecordSharingStatus.Approved)
            throw new InvalidOperationException("Only an approved request can be revoked.");

        Status = RecordSharingStatus.Revoked;
        RevokedAt = UpdatedDate = now;
        DecisionByUserId = userId;
    }
}
