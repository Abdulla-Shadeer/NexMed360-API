using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
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
        public async Task<ActionResult<Result<LoginResponseDto>>> Login(LoginUserDto userDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            try
            {
                var loginResponse = await _authService.LoginAsync(userDto);
                if (loginResponse.IsSuccess)
                {
                    return Ok(loginResponse);
                }
                return Unauthorized(loginResponse);
            }
            catch (Exception e)
            {
                return StatusCode(
                500,
                Result<string>.Failure(
                new ApiError(ErrorCode.Internal, $"An error occurred while processing the request.", 500, e.Message)
            )
        );
            }
        }

        [HttpGet]
        [Route("GetUser")]
        [Authorize]
        public async Task<ActionResult<Result<ViewUserDto>>> GetUser()
        {
            try
            {
                var authResult = await _authService.GetUserAsync(User);
                if (authResult.IsSuccess)
                {
                    return Ok(authResult);
                }
                return Unauthorized(authResult);
            }
            catch (Exception e)
            {
                return StatusCode(
                    500,
                    Result<ViewUserDto>
                    .Failure(new ApiError(ErrorCode.Internal, "An error occurred while processing the request!", 500, e.Message))
                    );
            }
        }
    }
}
