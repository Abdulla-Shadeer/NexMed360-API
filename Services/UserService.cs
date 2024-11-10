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

        public UserService(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, ILogger<UserService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task<string> RegisterUser(RegisterUserDto user)
        {
            User newUser = new User();
            newUser.FirstName = user.FirstName;
            newUser.LastName = user.LastName;
            newUser.UserName = user.Email;
            newUser.Email = user.Email;
            newUser.PhoneNumber = user.Phone;
            user.CreatedDate = DateTime.Now;
            user.UpdatedDate = DateTime.Now;
            newUser.CreatedDate = (DateTime)user.CreatedDate;
            newUser.UpdatedDate = (DateTime)user.UpdatedDate;
            
            var identityResult = await _userManager.CreateAsync(newUser, user.password);

            if(!identityResult.Succeeded) {
                _logger.LogError(IdentityErrorExtension.IdentityError(identityResult));
                throw new ApplicationException("User registration failed!");
            }
            var role = await _roleManager.FindByNameAsync(user.Role);
            if (role != null)
            {
                await _userManager.AddToRoleAsync(newUser, role.Name!);
                return (await _userManager.FindByEmailAsync(user.Email))!.Id;
            }
            else {
                _logger.LogError("No roles found!");
                return null;
            }
        }
    }
}
