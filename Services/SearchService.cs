using AutoMapper;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Dtos.Hospital;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;

namespace NexMed360Api.Services;

public class SearchService : ISearchService
{
    private readonly List<KeyValuePair<EntityTypes, List<string>>> requiredRolesToSearch = [
        new(EntityTypes.Patient, [UserRoles.DOCTOR, UserRoles.RECEPTIONIST]), 
        new(EntityTypes.Doctor, [UserRoles.PATIENT, UserRoles.HOSPITAL_ADMIN, UserRoles.RECEPTIONIST, UserRoles.DOCTOR, UserRoles.SUPERUSER]),
        new(EntityTypes.Hospital, [UserRoles.PATIENT, UserRoles.HOSPITAL_ADMIN, UserRoles.RECEPTIONIST, UserRoles.DOCTOR, UserRoles.SUPERUSER])
        ];
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<SearchService> _logger;
    private readonly IUserService _userService;

    public SearchService(
        ApplicationDbContext context, 
        IMapper mapper,
        ILogger<SearchService> logger,
        IUserService userService)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
        _userService = userService;
    }

    public async Task<Result<object>> EntitySearch(EntitySearchDto dto, ClaimsPrincipal user)
    {
        // user has roles
        // each entity search require specific roles
        // search operation should detect the requested entity and its role requirements
        // check if user has any of those required roles
        // return result if ok, else return unauthorized
        if (requiredRolesToSearch.FirstOrDefault(item => item.Key == dto.Entity).Value.Any(role => user.IsInRole(role))) {
            return dto.Entity switch
            {
                EntityTypes.Patient => Result<object>.Success(await PatientSearch(dto.InputType, dto.Input)),
                EntityTypes.Doctor => Result<object>.Success(await DoctorSearch(dto.InputType, dto.Input)),
                EntityTypes.Hospital => Result<object>.Success(await HospitalSearch(dto.Input)),
                _ => Result<object>.Failure(new ApiError(ErrorCode.SYS_BadRequest, $"Unknown Entity type \"{dto.Entity}\"", 400)),
            };
        }
        _logger.LogInformation($"User don't have required roles to search {dto.Entity}");
        return Result<object>.Failure(new ApiError(ErrorCode.SYS_Unauthorized, $"You're not authorized to view {dto.Entity} data.", 401));
    }

    private async Task<List<ViewUserDto>> PatientSearch(InputTypes type, string input) { 
        var foundUserIds = await _context.Patients
            .Where(p => type == InputTypes.Email ?
                EF.Functions.ILike(p.User.Email!, $"%{input}%") :
                EF.Functions.ILike(string.Join(" ", p.User.FirstName, p.User.LastName), $"%{input}%")
                )
            .Select(p => p.UserId)
            .ToListAsync();

        var viewUserDtos = new List<ViewUserDto>();

        if (foundUserIds.Count > 0)
        {
            foreach (var id in foundUserIds)
            {
                viewUserDtos.Add((await _userService.GetUserAsync(id)).Data!);
            }
        }

        return viewUserDtos;
    }

    private async Task<List<ViewUserDto>> DoctorSearch(InputTypes type, string input)
    {
        var foundUserIds = await _context.Doctors
            .Where(d => type == InputTypes.Email ?
                EF.Functions.ILike(d.User.Email!, $"%{input}%") :
                EF.Functions.ILike(string.Join(" ", d.User.FirstName, d.User.LastName), $"%{input}%")
                )
            .Select(d => d.UserId)
            .ToListAsync();

        var viewUserDtos = new List<ViewUserDto>();

        if (foundUserIds.Count > 0)
        {
            foreach (var id in foundUserIds)
            {
                viewUserDtos.Add((await _userService.GetUserAsync(id)).Data!);
            }
        }

        return viewUserDtos;
    }

    private async  Task<List<ViewHospitalDto>> HospitalSearch(string input) 
    {
        return await _context.Hospitals
            .Where(h => EF.Functions.ILike(h.Name, $"%{input}%"))
            .Select(h => _mapper.Map<ViewHospitalDto>(h))
            .ToListAsync();
    }

    public async Task<Result<List<ViewDoctorDto>>> SearchDoctor(SearchDoctorDto dto)
    {
        if (String.IsNullOrEmpty(dto.Name) && String.IsNullOrEmpty(dto.Email) && string.IsNullOrEmpty(dto.Speciality))
            return Result<List<ViewDoctorDto>>.Failure(ErrorCode.SYS_BadRequest, "Empty filter!");

        return Result<List<ViewDoctorDto>>.Success(
            await _context.Doctors
            .Include(dr => dr.User)
            .Include(dr => dr.Hospital)
            .Where(dr =>
                (String.IsNullOrEmpty(dto.Name) || EF.Functions.ILike(string.Join(" ", dr.User.FirstName, dr.User.LastName), $"%{dto.Name}%")) &&
                (String.IsNullOrEmpty(dto.Email) || EF.Functions.ILike(dr.User.Email!, $"%{dto.Email}%")) &&
                (String.IsNullOrEmpty(dto.Speciality) || EF.Functions.ILike(dr.Speciality, $"%{dto.Speciality}%")))
            .Select(dr => _mapper.Map<ViewDoctorDto>(dr))
            .ToListAsync()
            );
    }
}
