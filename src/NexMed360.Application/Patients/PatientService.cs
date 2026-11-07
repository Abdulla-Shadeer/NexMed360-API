using NexMed360.Application.Shared;
using NexMed360Api.Entities;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.ClinicRecord;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class PatientService(IAuthService auth, IPatientRepository data, IRegistrationTransactions transactions, PortalViews views, IClinicRecordRepository records) : IPatientService
{
    public async Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto)
    {
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.PATIENT);
        if (!registered.IsSuccess) return Result<ViewPatientDto>.Failure(registered.Error!);
        var patient = await data.Add(new Patient { UserId = registered.Data!.Id, Gender = dto.Gender, BirthDate = dto.BirthDate });
        var result = Result<ViewPatientDto>.Success(await views.Patient(patient));
        await transaction.CommitAsync(); return result;
    }
    public async Task<Result<ViewPatientDto>> GetPatient(string id)
    {
        if (!Guid.TryParse(id, out var patientId)) return Result<ViewPatientDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid 'patientId' format received!");
        if (!await data.PatientExists(patientId)) return Result<ViewPatientDto>.Failure(ErrorCode.USR_NotFound, $"Patient with id '{patientId}' not found!");
        return Result<ViewPatientDto>.Success(await views.Patient(await data.Find(patientId)));
    }
    public async Task<Result<ViewClinicRecordDto>> AddToClinicRecords(AddClinicRecordDto dto, string doctorUserId)
    {
        var doctorId = await data.FindDoctorId(doctorUserId);
        if (!Guid.TryParse(dto.PatientId, out var patientId))
            return Result<ViewClinicRecordDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid 'patientId' format received!");
        if (!await data.PatientExists(patientId))
            return Result<ViewClinicRecordDto>.Failure(ErrorCode.USR_NotFound, $"Patient with id '{patientId}' not found!");
        var prescriptions = dto.Prescriptions?.Select(value => new Prescription
        {
            MedicineName = value.MedicineName, Dosage = value.Dosage, TimesPerDay = value.TimesPerDay,
            IsLongTermUse = value.IsLongTermUse ?? false, Duration = value.Duration, Notes = value.Notes
        }).ToList() ?? [];
        var record = ClinicRecord.Create(patientId, doctorId, dto.Diagnosis, dto.DoctorNotes, dto.FollowUpDate, prescriptions);
        return Result<ViewClinicRecordDto>.Success(await views.Record(await records.Add(record)));
    }
    public async Task<Result<ViewClinicRecordDto>> ViewClinicRecord(string id, string? caller, bool isDoctor)
    {
        if (string.IsNullOrWhiteSpace(caller)) return Result<ViewClinicRecordDto>.Failure(ErrorCode.SYS_Unauthorized);
        if (!Guid.TryParse(id, out var recordId)) return Result<ViewClinicRecordDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid clinic record ID format.");
        var record = await records.FindReadable(recordId, caller, isDoctor);
        if (record == null) return Result<ViewClinicRecordDto>.Failure(ErrorCode.CLNRD_NotFound, $"Clinic record with ID '{id}' not found.");
        return Result<ViewClinicRecordDto>.Success(await views.Record(record));
    }
    public async Task<Result<List<ViewClinicRecordDto>>> ViewAllClinicRecords(string patientId, string? caller, bool isDoctor)
    {
        if (string.IsNullOrWhiteSpace(caller)) return Result<List<ViewClinicRecordDto>>.Failure(ErrorCode.SYS_Unauthorized);
        if (!Guid.TryParse(patientId, out var id)) return Result<List<ViewClinicRecordDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid patient ID format.");
        if (!await data.CanReadPatient(id, caller, isDoctor)) return Result<List<ViewClinicRecordDto>>.Failure(ErrorCode.USR_NotFound, $"Patient with ID '{patientId}' not found.");
        return Result<List<ViewClinicRecordDto>>.Success(await views.Records(await records.GetForPatient(id, caller, isDoctor)));
    }
}
