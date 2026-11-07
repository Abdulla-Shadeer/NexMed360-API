using NexMed360.Application.Shared;
using NexMed360Api.Entities;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class PatientService(IAuthService auth, IPatientRepository data, IRegistrationTransactions transactions, PortalViews views, IClinicalRecordRepository records) : IPatientService
{
    public async Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto)
    {
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.PATIENT);
        if (!registered.IsSuccess) return Result<ViewPatientDto>.Failure(registered.Error!);
        var patient = await data.Add(new Patient { PersonId = Guid.Parse(registered.Data!.PersonId), Gender = dto.Gender, BirthDate = dto.BirthDate });
        var result = Result<ViewPatientDto>.Success(await views.Patient(patient));
        await transaction.CommitAsync(); return result;
    }
    public async Task<Result<ViewPatientDto>> GetPatient(string id)
    {
        if (!Guid.TryParse(id, out var patientId)) return Result<ViewPatientDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid 'patientId' format received!");
        if (!await data.PatientExists(patientId)) return Result<ViewPatientDto>.Failure(ErrorCode.USR_NotFound, $"Patient with id '{patientId}' not found!");
        return Result<ViewPatientDto>.Success(await views.Patient(await data.Find(patientId)));
    }
    public async Task<Result<ViewClinicalRecordDto>> AddToClinicalRecords(AddClinicalRecordDto dto, string practitionerRoleUserId)
    {
        var role = await data.FindPractitionerRole(dto.PractitionerRoleId, practitionerRoleUserId);
        if (role == null)
            return Result<ViewClinicalRecordDto>.Failure(ErrorCode.SYS_Unauthorized, "Choose one of your active practitioner assignments.", 403);
        var practitionerRoleId = role.Id;
        if (!Guid.TryParse(dto.PatientId, out var patientId))
            return Result<ViewClinicalRecordDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid 'patientId' format received!");
        if (!await data.PatientExists(patientId))
            return Result<ViewClinicalRecordDto>.Failure(ErrorCode.USR_NotFound, $"Patient with id '{patientId}' not found!");
        var prescriptions = dto.Prescriptions?.Select(value => new Prescription
        {
            MedicineName = value.MedicineName, Dosage = value.Dosage, TimesPerDay = value.TimesPerDay,
            IsLongTermUse = value.IsLongTermUse ?? false, Duration = value.Duration, Notes = value.Notes
        }).ToList() ?? [];
        var record = ClinicalRecord.Create(patientId, practitionerRoleId, dto.Diagnosis, dto.PractitionerNotes, dto.FollowUpDate, prescriptions);
        record.OrganizationId = role.OrganizationId;
        foreach (var prescription in prescriptions) prescription.OrganizationId = role.OrganizationId;
        return Result<ViewClinicalRecordDto>.Success(await views.Record(await records.Add(record)));
    }
    public async Task<Result<ViewClinicalRecordDto>> ViewClinicalRecord(string id, string? caller, bool isPractitionerRole)
    {
        if (string.IsNullOrWhiteSpace(caller)) return Result<ViewClinicalRecordDto>.Failure(ErrorCode.SYS_Unauthorized);
        if (!Guid.TryParse(id, out var recordId)) return Result<ViewClinicalRecordDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid clinic record ID format.");
        var record = await records.FindReadable(recordId, caller, isPractitionerRole);
        if (record == null) return Result<ViewClinicalRecordDto>.Failure(ErrorCode.CLNRD_NotFound, $"Clinic record with ID '{id}' not found.");
        return Result<ViewClinicalRecordDto>.Success(await views.Record(record));
    }
    public async Task<Result<List<ViewClinicalRecordDto>>> ViewAllClinicalRecords(string patientId, string? caller, bool isPractitionerRole)
    {
        if (string.IsNullOrWhiteSpace(caller)) return Result<List<ViewClinicalRecordDto>>.Failure(ErrorCode.SYS_Unauthorized);
        if (!Guid.TryParse(patientId, out var id)) return Result<List<ViewClinicalRecordDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid patient ID format.");
        if (!await data.CanReadPatient(id, caller, isPractitionerRole)) return Result<List<ViewClinicalRecordDto>>.Failure(ErrorCode.USR_NotFound, $"Patient with ID '{patientId}' not found.");
        return Result<List<ViewClinicalRecordDto>>.Success(await views.Records(await records.GetForPatient(id, caller, isPractitionerRole)));
    }
}
