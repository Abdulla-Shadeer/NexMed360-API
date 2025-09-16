using AutoMapper;
using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Data;
using FinalProjectApi.Dtos.ClinicRecord;
using FinalProjectApi.Dtos.Patient;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Entities;
using FinalProjectApi.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectApi.Services;

public class PatientService : IPatientService
{
    private readonly IUserService _userService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;

    public PatientService(IUserService userService, ApplicationDbContext dbContext, IMapper mapper)
    {
        _userService = userService;
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto)
    {
        var userRegistration = await _userService.RegisterUser(dto.PersonalInformation, UserRoles.PATIENT);
        if (!userRegistration.IsSuccess)
            return Result<ViewPatientDto>.Failure(userRegistration.Error!);

        var patientEntry = new Patient()
        {
            UserId = userRegistration.Data!.Id,
            Gender = dto.Gender,
            BirthDate = dto.BirthDate
        };

        await _dbContext.Patients.AddAsync(patientEntry);
        await _dbContext.SaveChangesAsync();

        var patient = await _dbContext.Patients
            .Where(p => p.UserId == userRegistration.Data.Id)
            .Include(p => p.User)
            .Select(p => _mapper.Map<ViewPatientDto>(p))
            .FirstOrDefaultAsync();

        return Result<ViewPatientDto>.Success(patient);
    }

    public async Task<Result<ViewPatientDto>> GetPatient(string id)
    {
        var patientCheck = await UserCheck(id);
        if (!patientCheck.IsSuccess)
            return Result<ViewPatientDto>.Failure(patientCheck.Error!);

        var patient = await _dbContext.Patients
            .Where(p => p.Id == patientCheck.Data)
            .Include(p => p.User)
            .Select(p => _mapper.Map<ViewPatientDto>(p))
            .FirstOrDefaultAsync();

        return Result<ViewPatientDto>.Success(patient);
    }

    public async Task<Result<ViewClinicRecordDto>> AddToClinicRecords(AddClinicRecordDto dto, string doctorUserId)
    {
        var doctorId = await _dbContext.Doctors
            .Where(d => d.UserId == doctorUserId)
            .Select(d => d.Id).FirstOrDefaultAsync();

        var patientCheck = await UserCheck(dto.PatientId);
        if (!patientCheck.IsSuccess)
            return Result<ViewClinicRecordDto>.Failure(patientCheck.Error!);

        var clinicRec = _mapper.Map<ClinicRecord>(dto);
        clinicRec.DoctorId = doctorId;

        var clinicRecordEntry = await _dbContext.ClinicRecords.AddAsync(clinicRec);
        await _dbContext.SaveChangesAsync();

        var addedClinicRecord = await _dbContext.ClinicRecords
            .Include(cr => cr.Doctor)
            .ThenInclude(dr => dr.User)
            .Include(cr => cr.Doctor)
            .ThenInclude(dr => dr.Hospital)
            .Include(cr => cr.Patient)
            .ThenInclude(pt => pt.User)
            .Include(cr => cr.Prescriptions)
            .FirstOrDefaultAsync(cr => cr.Id == clinicRecordEntry.Entity.Id);

        return Result<ViewClinicRecordDto>.Success(_mapper.Map<ViewClinicRecordDto>(addedClinicRecord));
    }

    public async Task<Result<ViewClinicRecordDto>> ViewClinicRecord(string id)
    {
        if (!Guid.TryParse(id, out var clinicRecordId))
            return Result<ViewClinicRecordDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid clinic record ID format.");

        var clinicRecord = await _dbContext.ClinicRecords
            .Include(cr => cr.Doctor)
            .ThenInclude(dr => dr.User)
            .Include(cr => cr.Doctor)
            .ThenInclude(dr => dr.Hospital)
            .Include(cr => cr.Patient)
            .ThenInclude(pt => pt.User)
            .Include(cr => cr.Prescriptions)
            .FirstOrDefaultAsync(cr => cr.Id == clinicRecordId);

        if (clinicRecord == null)
            return Result<ViewClinicRecordDto>.Failure(ErrorCode.CLNRD_NotFound, $"Clinic record with ID '{id}' not found.");

        return Result<ViewClinicRecordDto>.Success(_mapper.Map<ViewClinicRecordDto>(clinicRecord));
    }

    public async Task<Result<List<ViewClinicRecordDto>>> ViewAllClinicRecords(string patientId)
    {
        if (!Guid.TryParse(patientId, out var id))
            return Result<List<ViewClinicRecordDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid patient ID format.");

        if (!await _dbContext.Patients.AnyAsync(p => p.Id == id))
            return Result<List<ViewClinicRecordDto>>.Failure(ErrorCode.USR_NotFound, $"Patient with ID '{patientId}' not found.");

        var clinicRecords = await _dbContext.ClinicRecords
            .Where(cr => cr.PatientId == id)
            .Include(cr => cr.Doctor)
            .ThenInclude(dr => dr.User)
            .Include(cr => cr.Doctor)
            .ThenInclude(dr => dr.Hospital)
            .Include(cr => cr.Patient)
            .ThenInclude(pt => pt.User)
            .Include(cr => cr.Prescriptions)
            .ToListAsync();

        return Result<List<ViewClinicRecordDto>>.Success(
            clinicRecords.Select(cr => _mapper.Map<ViewClinicRecordDto>(cr)).ToList()
        );
    }

    private async Task<Result<Guid>> UserCheck(string userId, bool isDoctor = false)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Result<Guid>.Failure(ErrorCode.SYS_InvalidGuid, isDoctor ? "Invalid 'doctorId' format received!" : "Invalid 'patientId' format received!");

        if (isDoctor ? !await _dbContext.Doctors.AnyAsync(dr => dr.Id == userGuid) : !await _dbContext.Patients.AnyAsync(pt => pt.Id == userGuid))
            return Result<Guid>.Failure(ErrorCode.USR_NotFound, isDoctor ? $"Doctor with id '{userGuid}' not found!" : $"Patient with id '{userGuid}' not found!");

        return Result<Guid>.Success(userGuid);
    }
}
