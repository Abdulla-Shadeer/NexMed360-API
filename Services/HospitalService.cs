using AutoMapper;
using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Data;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;
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

    public async Task<Result<ViewHospitalAdminDto>> CreateHospitalAdmin(CreateHospitalAdminDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId))
            return Result<ViewHospitalAdminDto>.Failure(ErrorCode.SYS_InvalidGuid);

        if (!await _dbContext.Hospitals.AnyAsync(h => h.Id == hospitalId))
            return Result<ViewHospitalAdminDto>.Failure(ErrorCode.HOS_NotFound);

        var userRegistration = await _userService.RegisterUser(dto.PersonalInformation, UserRoles.HOPITAL_ADMIN);
        if (!userRegistration.IsSuccess)
            return Result<ViewHospitalAdminDto>.Failure(userRegistration.Error!);


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
            .Include(ha => ha.Hospital)
            .Include(ha => ha.User)
            .FirstOrDefaultAsync(ha => ha.UserId == userRegistration.Data.Id);

        return Result<ViewHospitalAdminDto>.Success(_mapper.Map<ViewHospitalAdminDto>(hospitalAdmin));
    }

    public async Task<Result<ViewReceptionistDto>> CreateReceptionist(CreateReceptionistDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId))
            return Result<ViewReceptionistDto>.Failure(ErrorCode.SYS_InvalidGuid);

        if (!await _dbContext.Hospitals.AnyAsync(h => h.Id == hospitalId))
            return Result<ViewReceptionistDto>.Failure(ErrorCode.HOS_NotFound);

        var userRegistration = await _userService.RegisterUser(dto.PersonalInformation, UserRoles.RECEPTIONIST);

        if (!userRegistration.IsSuccess)
            return Result<ViewReceptionistDto>.Failure(userRegistration.Error!);

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
            .Include(r => r.Hospital)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == userRegistration.Data.Id);
        return Result<ViewReceptionistDto>.Success(_mapper.Map<ViewReceptionistDto>(receptionist));
    }

    public async Task<Result<ViewDoctorDto>> CreateDoctor(CreateDoctorDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId))
            return Result<ViewDoctorDto>.Failure(ErrorCode.SYS_InvalidGuid);

        if (!await _dbContext.Hospitals.AnyAsync(h => h.Id == hospitalId))
            return Result<ViewDoctorDto>.Failure(ErrorCode.HOS_NotFound);

        var userRegistrationResult = await _userService.RegisterUser(dto.PersonalInformation, UserRoles.DOCTOR);
        if (!userRegistrationResult.IsSuccess)
            return Result<ViewDoctorDto>.Failure(userRegistrationResult.Error!);

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

        var doctor = await _dbContext.Doctors
            .Include(dr => dr.Hospital)
            .Include(dr => dr.User)
            .FirstOrDefaultAsync(dr => dr.UserId == userRegistrationResult.Data!.Id);
        return Result<ViewDoctorDto>.Success(_mapper.Map<ViewDoctorDto>(doctor));
    }
}
