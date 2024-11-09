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
        private readonly IUserService _userService;

        public UsersController(IAuthService authService, IUserService userService)
        {
            _authService = authService;
            _userService = userService;
        }

        [HttpPost]
        [Route("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<String>> Login(LoginUserDto userDto)
        {
            if (await _authService.ValidateUser(userDto))
            {
                return Ok(new { token = await _authService.GenerateToken() });
            }
            return Unauthorized(new { error = "Invalid username or password!" });
        }

        [HttpPost]
        [Route("Register")]
        [AllowAnonymous]
        public async Task<ActionResult<RegisterUserDto>> Register(RegisterUserDto userDto)
        {
            var createdUser = await _userService.RegisterUser(userDto);

            if (createdUser != null)
            {
                return Ok(createdUser);
            }
            return UnprocessableEntity(userDto);
        }
    }
}
