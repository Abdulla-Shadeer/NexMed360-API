using FinalProjectApi.Common.Auth;
using FinalProjectApi.Entities;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Identity;

namespace FinalProjectApi.Services
{
    public class DbInitializerService : IDbInitializerService
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<DbInitializerService> _logger;
        private readonly IConfiguration _configuration;

        public DbInitializerService(RoleManager<IdentityRole> roleManager, UserManager<User> userManager, ILogger<DbInitializerService> logger, IConfiguration configuration)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SeedData() { 
            await InitializeRoles();
            await InitializeUsers();
        }

        private async Task InitializeRoles()
        {
            _logger.LogInformation("Seeding user roles..");
            await _roleManager.CreateAsync(new IdentityRole(UserRoles.SUPERUSER));
            await _roleManager.CreateAsync(new IdentityRole(UserRoles.HOPITAL_ADMIN));
            await _roleManager.CreateAsync(new IdentityRole(UserRoles.RECEPTIONIST));
            await _roleManager.CreateAsync(new IdentityRole(UserRoles.DOCTOR));
            await _roleManager.CreateAsync(new IdentityRole(UserRoles.PATIENT));
        }

        private async Task InitializeUsers()
        {
            User superUser = new User();
            superUser.FirstName = "Super";
            superUser.LastName = "User";
            superUser.Email = "superuser@gmail.com";
            superUser.CreatedDate = DateTime.Now;
            superUser.UpdatedDate = DateTime.Now;
            superUser.PhoneNumber = "0123456789";
            superUser.UserName = "superuser@gmail.com";

            var superUserPassword = _configuration["Auth:SuperUserPassword"]!;

            var res = await _userManager.CreateAsync(superUser, superUserPassword);
            if (res.Succeeded)
            {
                await _userManager.AddToRoleAsync(superUser, UserRoles.SUPERUSER);
            }
            else {
                _logger.LogWarning("Failed to seed users");
                _logger.LogInformation(res.ToString());
            }
        }
    }
}
