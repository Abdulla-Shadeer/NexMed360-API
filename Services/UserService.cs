using AutoMapper;
using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Entities;
using FinalProjectApi.Extensions;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Identity;

namespace FinalProjectApi.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserService> _logger;
        private readonly IMapper _mapper;

        public UserService(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, ILogger<UserService> logger, IMapper mapper)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto user, string role)
        {
            var identityRole = await _roleManager.FindByNameAsync(role);
            if (identityRole == null)
                return Result<ViewUserDto>.Failure(ErrorCode.USR_RoleNotFound, $"The {role} role does not exist.");

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
            return Result<ViewUserDto>.Success(_mapper.Map<ViewUserDto>(await _userManager.FindByEmailAsync(user.Email)));
        }
    }
}
