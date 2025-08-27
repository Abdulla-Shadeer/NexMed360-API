using AutoMapper;
using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Data;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Overview;
using FinalProjectApi.Dtos.Patient;
using FinalProjectApi.Dtos.Receptionist;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Entities;
using FinalProjectApi.Extensions;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinalProjectApi.Services;

public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<UserService> _logger;
    private readonly IMapper _mapper;
    private readonly IOverviewService _overviewService;
    private readonly ApplicationDbContext _context;

    public UserService(
        UserManager<User> userManager, 
        RoleManager<IdentityRole> roleManager, 
        ILogger<UserService> logger, 
        IMapper mapper, 
        IOverviewService overviewService,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _mapper = mapper;
        _overviewService = overviewService;
        _context = context;
    }

    public async Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto user, string role)
    {
        var identityRole = await _roleManager.FindByNameAsync(role);
        if (identityRole == null)
            return Result<ViewUserDto>.Failure(ErrorCode.USR_RoleNotFound, $"The {role} role does not exist.");

        // create user if no user already exists with the email
        // add only to new role if a user profile already exists
        var existingUser = await _userManager.FindByEmailAsync(user.Email);
        if (existingUser != null)
        {
            if (!await _userManager.IsInRoleAsync(existingUser, role))
            {
                await _userManager.AddToRoleAsync(existingUser, role);
                var updatedRoles = await _userManager.GetRolesAsync(existingUser);
                var viewUserDto = _mapper.Map<ViewUserDto>(existingUser);
                viewUserDto.Roles = updatedRoles.ToList();
                _logger.LogInformation($"User already exists, assigned to new role: '{role}'");
                return Result<ViewUserDto>.Success(viewUserDto);
            }

            var error = $"User already exists and already has the '{role}' role.";
            _logger.LogError(error);
            return Result<ViewUserDto>.Failure(ErrorCode.USR_RegistrationFailed, error, 400);
        }

        var newUser = _mapper.Map<User>(user);

        var passwordValidationErrors = new List<IdentityError>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, newUser, user.Password);
            if (!validationResult.Succeeded)
                passwordValidationErrors.AddRange(validationResult.Errors);
        }

        if (passwordValidationErrors.Count > 0)
        {
            string stringErrors = string.Join(Environment.NewLine, passwordValidationErrors.Select(e => $"{e.Code}: {e.Description}"));
            return Result<ViewUserDto>.Failure(ErrorCode.USR_RegistrationFailed, stringErrors, 400);
        }

        var identityResult = await _userManager.CreateAsync(newUser, user.Password);

        if (!identityResult.Succeeded)
        {
            var errDetail = IdentityErrorExtension.IdentityError(identityResult);
            _logger.LogError(errDetail);
            Result<ViewUserDto>.Failure(ErrorCode.USR_RegistrationFailed, errDetail);
        }

        await _userManager.AddToRoleAsync(newUser, role);
        var userId = await _userManager.GetUserIdAsync(newUser);
        return await GetUserAsync(userId);
    }

    public async Task<Result<List<OverviewDto>>> GetOverview(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Result<List<OverviewDto>>.Failure(ErrorCode.SYS_BadRequest, "The received token is missing a valid 'sub' claim");

        var isSuperUser = user.IsInRole(UserRoles.SUPERUSER);
        var isHospitalAdmin = user.IsInRole(UserRoles.HOSPITAL_ADMIN);
        var isReceptionist = user.IsInRole(UserRoles.RECEPTIONIST);
        var isDoctor = user.IsInRole(UserRoles.DOCTOR);
        var isPatient = user.IsInRole(UserRoles.PATIENT);

        List<OverviewDto> overviews = [];

        if (isSuperUser)
            overviews.Add(await _overviewService.GetSystemOverview());

        if (isReceptionist || isHospitalAdmin)
            overviews.Add(await _overviewService.GetHospitalOverview(userId, isHospitalAdmin));

        if (isDoctor)
            overviews.Add(await _overviewService.GetDoctorOverview(userId));

        if (isPatient)
            overviews.Add(await _overviewService.GetPatientOverview(userId));

        return Result<List<OverviewDto>>.Success(overviews);
    }

    public async Task<Result<ViewUserDto>> GetUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var viewUserDto = _mapper.Map<ViewUserDto>(user);
            viewUserDto.Roles = (await _userManager.GetRolesAsync(user)).ToList();

            if (viewUserDto.Roles.Any(role => role == UserRoles.PATIENT))
            {
                viewUserDto.PatientProfile = await _context.Patients
                    .Where(p => p.UserId == userId)
                    .Select(p => _mapper.Map<ViewPatientDto>(p))
                    .FirstOrDefaultAsync();
            }

            if (viewUserDto.Roles.Any(role => role == UserRoles.DOCTOR))
            {
                viewUserDto.DoctorProfile = await _context.Doctors
                    .Where(d => d.UserId == userId)
                    .Include(d => d.Hospital)
                    .Select(d => _mapper.Map<ViewDoctorDto>(d))
                    .FirstOrDefaultAsync();
            }

            if (viewUserDto.Roles.Any(role => role == UserRoles.RECEPTIONIST))
            {
                viewUserDto.ReceptionistProfile = await _context.Receptionists
                    .Where(r => r.UserId == userId)
                    .Include(r => r.Hospital)
                    .Select(r => _mapper.Map<ViewReceptionistDto>(r))
                    .FirstOrDefaultAsync();
            }

            if (viewUserDto.Roles.Any(role => role == UserRoles.HOSPITAL_ADMIN))
            {
                viewUserDto.HospitalAdminProfile = await _context.HospitalAdmins
                    .Where(hsAdmn => hsAdmn.UserId == userId)
                    .Include(hsAdmn => hsAdmn.Hospital)
                    .Select(hsAdmn => _mapper.Map<ViewHospitalAdminDto>(hsAdmn))
                    .FirstOrDefaultAsync();
            }

            return Result<ViewUserDto>.Success(viewUserDto);
        }
        else
        {
            return Result<ViewUserDto>.Failure(ErrorCode.SYS_Unauthorized, $"User with id {userId} not found.");
        }
    }
}
