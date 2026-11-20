using NexMed360.Application.Access;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Application.Patients;

public sealed class RecordSharingService(IRecordSharingRepository data, IPatientRepository patients,
    IPractitionerRoleRepository practitionerRoles, WorkspaceAccess workspace)
{
    public async Task<Result<Guid>> Create(CreateRecordSharingRequestDto dto, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || workspace.Mode != WorkspaceModes.Work
            || !workspace.OrganizationId.HasValue || !workspace.Has(Permissions.ClinicalRecordsRead))
            return Denied();

        var role = await practitionerRoles.FindActiveForUser(dto.PractitionerRoleId, userId);
        if (role == null || role.OrganizationId != workspace.OrganizationId || !workspace.CanUseAssignment(role.Id))
            return ApiError.From(ErrorCode.SYS_Unauthorized, "Choose one of your active practitioner assignments.", 403);
        if (!await patients.PatientExists(dto.PatientId))
            return ApiError.From(ErrorCode.USR_NotFound, "Patient not found.");
        if (!await data.PatientHasPortalAccess(dto.PatientId))
            return ApiError.From(ErrorCode.SYS_BadRequest, "This patient needs a portal account before they can approve sharing.", 409);
        if (string.IsNullOrWhiteSpace(dto.Purpose) || dto.Purpose.Trim().Length > 1000)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Enter a purpose of up to 1000 characters.");
        if (dto.RecordFromDate.HasValue && dto.RecordToDate.HasValue && dto.RecordFromDate > dto.RecordToDate)
            return ApiError.From(ErrorCode.SYS_BadRequest, "The from date must be on or before the to date.");

        var now = DateTime.UtcNow;
        var accessExpiresOn = dto.AccessExpiresOn ?? DateOnly.FromDateTime(now.AddDays(30));
        var expiresAt = accessExpiresOn.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        if (expiresAt <= now)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Choose an access expiry in the future.");

        var request = new RecordSharingRequest
        {
            PatientId = dto.PatientId, ReceivingOrganizationId = workspace.OrganizationId.Value,
            PractitionerRoleId = role.Id, Purpose = dto.Purpose.Trim(), RecordFromDate = dto.RecordFromDate,
            RecordToDate = dto.RecordToDate, ExpiresAt = expiresAt, CreatedDate = now
        };
        await data.Add(request);
        return request.Id;
    }

    public async Task<Result<List<RecordSharingRequestDto>>> List(Guid? patientId, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Denied();
        if (workspace.Mode == WorkspaceModes.Patient && workspace.PatientId.HasValue
            && await patients.CanReadPatient(workspace.PatientId.Value, userId, false))
            return await data.ForPatient(workspace.PatientId.Value);
        if (workspace.Mode == WorkspaceModes.Work && workspace.OrganizationId.HasValue
            && workspace.Has(Permissions.ClinicalRecordsRead))
            return await data.ForOrganization(workspace.OrganizationId.Value, patientId);
        return Denied();
    }

    public async Task<Result<bool>> Decide(Guid id, bool approve, string? userId) =>
        await Change(id, userId, approve);

    public async Task<Result<bool>> Revoke(Guid id, string? userId) => await Change(id, userId, null);

    private async Task<Result<bool>> Change(Guid id, string? userId, bool? approve)
    {
        if (string.IsNullOrWhiteSpace(userId) || workspace.Mode != WorkspaceModes.Patient
            || !workspace.PatientId.HasValue || !await patients.CanReadPatient(workspace.PatientId.Value, userId, false))
            return Denied();
        var request = await data.FindForPatient(id, workspace.PatientId.Value);
        if (request == null) return ApiError.From(ErrorCode.USR_NotFound, "Sharing request not found.");

        try
        {
            if (approve.HasValue) request.Decide(approve.Value, userId, DateTime.UtcNow);
            else request.Revoke(userId, DateTime.UtcNow);
        }
        catch (InvalidOperationException error)
        {
            return ApiError.From(ErrorCode.SYS_BadRequest, error.Message, 409);
        }

        if (!await data.Save(request))
            return ApiError.From(ErrorCode.SYS_BadRequest, "This request has changed. Refresh the list and try again.", 409);
        return true;
    }

    private static ApiError Denied() => ApiError.From(ErrorCode.SYS_Unauthorized, "You do not have access in this workspace.", 403);
}
