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
using System.Text.Json;

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

    public async Task<Result<ViewHospitalDto>> Add(CreateHospitalDto dto)
    {
        var hospitalEntry = await _dbContext.Hospitals.AddAsync(_mapper.Map<Hospital>(dto));
        await _dbContext.SaveChangesAsync();

        return Result<ViewHospitalDto>.Success(_mapper.Map<ViewHospitalDto>(hospitalEntry.Entity));
    }

    public async Task<Result<ViewUserDto>> AssignAdmin(CreateHospitalAdminDto dto)
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

    public async Task<Result<ViewUserDto>> AddReceptionist(CreateReceptionistDto dto)
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

    public async Task<Result<ViewUserDto>> AddDoctor(CreateDoctorDto dto)
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

    public async Task<Result<List<ViewHospitalDto>>> GetAll()
    {
        var query = _dbContext.Hospitals
            .AsQueryable();

        var result = await query
            .Select(h => _mapper.Map<ViewHospitalDto>(h))
            .ToListAsync();

        return Result<List<ViewHospitalDto>>.Success(result);
    }

    public async Task<Result<ViewHospitalDto>> Update(UpdateHospitalDto updateDto)
    {
        if (!Guid.TryParse(updateDto.Id, out var hospitalId))
            return Result<ViewHospitalDto>.Failure(ErrorCode.SYS_InvalidGuid, "Hospital ID has invalid format!");

        var existingHospital = await _dbContext.Hospitals
            .FirstOrDefaultAsync(hs => hs.Id == hospitalId);

        if (existingHospital == null)
            return Result<ViewHospitalDto>.Failure(ErrorCode.HOS_NotFound);

        existingHospital.Name = updateDto.Name ?? existingHospital.Name;
        existingHospital.Street = updateDto.Street ?? existingHospital.Street;
        existingHospital.City = updateDto.City ?? existingHospital.City;
        existingHospital.District = updateDto.District ?? existingHospital.District;
        existingHospital.Phone = updateDto.Phone ?? existingHospital.Phone;
        existingHospital.Grade = updateDto.Grade ?? existingHospital.Grade;

        await _dbContext.SaveChangesAsync();
        return Result<ViewHospitalDto>.Success(_mapper.Map<ViewHospitalDto>(existingHospital));
    }

    public async Task<Result<ViewHospitalDto>> FindById(string Id, bool isSuperUser) { 
        if (!Guid.TryParse(Id, out var hospitalId))
            return Result<ViewHospitalDto>.Failure(ErrorCode.SYS_InvalidGuid, "Hospital ID has invalid format!");

        var query = _dbContext.Hospitals
            .Where(hs => hs.Id == hospitalId)
            .AsQueryable();

        if (isSuperUser)
        {
            query = query.Include(hs => hs.HospitalAdmins)
                .ThenInclude(admn => admn.User);
        }
        var hospital = await query
            .Select(hs => _mapper.Map<ViewHospitalDto>(hs))
            .FirstOrDefaultAsync();

        return Result<ViewHospitalDto>.Success(hospital);
    }

    public async Task<Result<List<ViewUserDto>>> GetMembers(string hospitalId, string userId)
    {
        if (!Guid.TryParse(hospitalId, out var _hospitalId))
            return Result<List<ViewUserDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid hospital ID format.");

        var query = _dbContext.Hospitals
            .Where(h => h.Id == _hospitalId)
            .AsQueryable();

        if (!await query.AnyAsync())
            return Result<List<ViewUserDto>>.Failure(ErrorCode.HOS_NotFound, $"Hospital with ID {hospitalId} not found!");

        if (!await query.Include(h => h.HospitalAdmins).AnyAsync(h => h.HospitalAdmins.Any(ha => ha.UserId == userId)))
            return Result<List<ViewUserDto>>.Failure(ErrorCode.SYS_Unauthorized, "Access denied, only admins of this hospital can view its members.");

        var membersObj = await query
            .Include(hs => hs.Doctors)
            .ThenInclude(dr => dr.User)
            .Include(hs => hs.Doctors)
            .ThenInclude(dr => dr.Hospital)
            .Include(hs => hs.Receptionists)
            .ThenInclude(rc => rc.User)
            .Include(hs => hs.Receptionists)
            .ThenInclude(rc => rc.Hospital)
            .Select(hs => new {
                doctors = hs.Doctors
                    .Select(dr => _mapper.Map<ViewUserDto>(dr)),
                receptionists = hs.Receptionists
                    .Select(rc => _mapper.Map<ViewUserDto>(rc)),
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync();

        List<ViewUserDto> members = [];
        members.AddRange(membersObj?.doctors ?? []);
        members.AddRange(membersObj?.receptionists ?? []);

        return Result<List<ViewUserDto>>.Success(members);
    }
}
