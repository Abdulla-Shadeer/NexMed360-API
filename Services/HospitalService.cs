using AutoMapper;
using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Data;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Entities;
using FinalProjectApi.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectApi.Services;

public class HospitalService : IHospitalService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public HospitalService(ApplicationDbContext dbContext, IUserService userService, IMapper mapper)
    {
        _dbContext = dbContext;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<Result<ViewHospitalDto>> CreateHospital(CreateHospitalDto dto)
    {
        var hospitalEntry = await _dbContext.Hospitals.AddAsync(_mapper.Map<Hospital>(dto));
        await _dbContext.SaveChangesAsync();

        return Result<ViewHospitalDto>.Success(_mapper.Map<ViewHospitalDto>(hospitalEntry.Entity));
    }

    public async Task<Result<ViewUserDto>> CreateHospitalAdmin(CreateHospitalAdminDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId))
            return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);

        if (!await _dbContext.Hospitals.AnyAsync(h => h.Id == hospitalId))
            return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);

        var userRegistration = await _userService.RegisterUser(dto.PersonalInformation, UserRoles.HOSPITAL_ADMIN);
        if (!userRegistration.IsSuccess)
            return userRegistration;


        var hospitalAdminEntry = new HospitalAdmin()
        {
            HospitalId = hospitalId,
            UserId = userRegistration.Data!.Id,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };

        await _dbContext.HospitalAdmins.AddAsync(hospitalAdminEntry);
        await _dbContext.SaveChangesAsync();

        var hospitalAdmin = await _dbContext.HospitalAdmins
            .Where(ha => ha.UserId == userRegistration.Data.Id)
            .Include(ha => ha.Hospital)
            .Select(ha => _mapper.Map<ViewHospitalAdminDto>(ha))
            .FirstOrDefaultAsync();

        userRegistration.Data.HospitalAdminProfile = hospitalAdmin;

        return userRegistration;
    }

    public async Task<Result<ViewUserDto>> CreateReceptionist(CreateReceptionistDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId))
            return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);

        if (!await _dbContext.Hospitals.AnyAsync(h => h.Id == hospitalId))
            return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);

        var userRegistration = await _userService.RegisterUser(dto.PersonalInformation, UserRoles.RECEPTIONIST);

        if (!userRegistration.IsSuccess)
            return userRegistration;

        var receptionistEntry = new Receptionist()
        {
            HospitalId = hospitalId,
            UserId = userRegistration.Data!.Id,
            Department = dto.Department,
            Location = dto.Location,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };

        await _dbContext.Receptionists.AddAsync(receptionistEntry);
        await _dbContext.SaveChangesAsync();

        var receptionist = await _dbContext.Receptionists
            .Where(r => r.UserId == userRegistration.Data.Id)
            .Include(r => r.Hospital)
            .Select(r => _mapper.Map<ViewReceptionistDto>(r))
            .FirstOrDefaultAsync();

        userRegistration.Data.ReceptionistProfile = receptionist;

        return userRegistration;
    }

    public async Task<Result<ViewUserDto>> CreateDoctor(CreateDoctorDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId))
            return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);

        if (!await _dbContext.Hospitals.AnyAsync(h => h.Id == hospitalId))
            return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);

        var userRegistrationResult = await _userService.RegisterUser(dto.PersonalInformation, UserRoles.DOCTOR);
        if (!userRegistrationResult.IsSuccess)
            return userRegistrationResult;

        var doctorEntry = new Doctor()
        {
            HospitalId = hospitalId,
            UserId = userRegistrationResult.Data!.Id,
            Speciality = dto.Speciality,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };

        await _dbContext.Doctors.AddAsync(doctorEntry);
        await _dbContext.SaveChangesAsync();

        userRegistrationResult.Data.DoctorProfile = await _dbContext.Doctors
            .Where(dr => dr.UserId == userRegistrationResult.Data!.Id)
            .Include(dr => dr.Hospital)
            .Select(dr => _mapper.Map<ViewDoctorDto>(dr))
            .FirstOrDefaultAsync();

        return userRegistrationResult;
    }
}
