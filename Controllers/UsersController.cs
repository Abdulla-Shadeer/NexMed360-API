using FinalProjectApi.Dtos.User;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinalProjectApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IAuthService _authService;

        public UsersController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost]
        [Route("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> Login(LoginUserDto userDto)
        {
            if (await _authService.ValidateUser(userDto))
            {
                return Ok(new { token = await _authService.GenerateToken() });
            }
            return Unauthorized(new { error = "Invalid username or password!" });
        }
    }
}
