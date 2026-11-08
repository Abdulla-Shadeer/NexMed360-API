using NexMed360.Application.Access;
using NexMed360Api.Entities;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class PatientService(IAuthService auth, IPatientRepository data, IRegistrationTransactions transactions, IClinicalRecordRepository records, WorkspaceAccess? workspace = null) : IPatientService
{
    public async Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto)
    {
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.PATIENT);
        if (!registered.IsSuccess) return registered.Error!;
        var patient = await data.Add(new Patient { PersonId = Guid.Parse(registered.Data!.PersonId), Gender = dto.Gender, BirthDate = dto.BirthDate });
        var result = await data.View(patient);
        await transaction.CommitAsync(); return result;
    }
    public async Task<Result<ViewPatientDto>> GetPatient(string id)
    {
        if (!Guid.TryParse(id, out var patientId)) return ApiError.From(ErrorCode.SYS_InvalidGuid, "Invalid 'patientId' format received!");
        if (!await data.PatientExists(patientId)) return ApiError.From(ErrorCode.USR_NotFound, $"Patient with id '{patientId}' not found!");
        return await data.View(await data.Find(patientId));
    }
    public async Task<Result<ViewClinicalRecordDto>> AddToClinicalRecords(AddClinicalRecordDto dto, string practitionerRoleUserId)
    {
        var role = await data.FindPractitionerRole(dto.PractitionerRoleId, practitionerRoleUserId);
        if (role == null)
            return ApiError.From(ErrorCode.SYS_Unauthorized, "Choose one of your active practitioner assignments.", 403);
        var practitionerRoleId = role.Id;
        if (!Guid.TryParse(dto.PatientId, out var patientId))
            return ApiError.From(ErrorCode.SYS_InvalidGuid, "Invalid 'patientId' format received!");
        if (!await data.PatientExists(patientId))
            return ApiError.From(ErrorCode.USR_NotFound, $"Patient with id '{patientId}' not found!");
        if (workspace?.IsSelected == true && (role.Id != workspace.PractitionerRoleId
            || dto.Prescriptions?.Count > 0 && !workspace.Has(Permissions.PrescriptionsIssue)))
            return ApiError.From(ErrorCode.SYS_Unauthorized, "Choose your current assignment and check your prescription permission.", 403);
        var prescriptions = dto.Prescriptions?.Select(value => new Prescription
        {
            MedicineName = value.MedicineName, Dosage = value.Dosage, TimesPerDay = value.TimesPerDay,
            IsLongTermUse = value.IsLongTermUse ?? false, Duration = value.Duration, Notes = value.Notes
        }).ToList() ?? [];
        var record = ClinicalRecord.Create(patientId, practitionerRoleId, dto.Diagnosis, dto.PractitionerNotes, dto.FollowUpDate, prescriptions);
        record.OrganizationId = role.OrganizationId;
        foreach (var prescription in prescriptions) prescription.OrganizationId = role.OrganizationId;
        return await records.View(await records.Add(record));
    }
    public async Task<Result<ViewClinicalRecordDto>> ViewClinicalRecord(string id, string? caller, bool isPractitionerRole)
    {
        if (string.IsNullOrWhiteSpace(caller)) return ErrorCode.SYS_Unauthorized;
        if (!Guid.TryParse(id, out var recordId)) return ApiError.From(ErrorCode.SYS_InvalidGuid, "Invalid clinic record ID format.");
        var record = await records.FindReadable(recordId, caller, isPractitionerRole);
        if (record == null) return ApiError.From(ErrorCode.CLNRD_NotFound, $"Clinic record with ID '{id}' not found.");
        return await records.View(record);
    }
    public async Task<Result<List<ViewClinicalRecordDto>>> ViewAllClinicalRecords(string patientId, string? caller, bool isPractitionerRole)
    {
        if (string.IsNullOrWhiteSpace(caller)) return ErrorCode.SYS_Unauthorized;
        if (!Guid.TryParse(patientId, out var id)) return ApiError.From(ErrorCode.SYS_InvalidGuid, "Invalid patient ID format.");
        if (!await data.CanReadPatient(id, caller, isPractitionerRole)) return ApiError.From(ErrorCode.USR_NotFound, $"Patient with ID '{patientId}' not found.");
        return await records.Views(await records.GetForPatient(id, caller, isPractitionerRole));
    }
    public async Task<Result<ViewPatientDto>> GetOwnPatient()
    {
        if (workspace?.Mode != WorkspaceModes.Patient || !workspace.PatientId.HasValue) return ErrorCode.SYS_Unauthorized;
        var patient = await data.View(await data.Find(workspace.PatientId.Value));
        return patient == null ? ErrorCode.USR_NotFound : patient;
    }

}
