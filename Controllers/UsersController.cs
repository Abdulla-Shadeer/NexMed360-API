using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Entities;
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
        public async Task<ActionResult<Result<string>>> Login(LoginUserDto userDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            try
            {
                if (await _authService.ValidateUser(userDto))
                {
                    return Ok(Result<string>.Success(await _authService.GenerateToken()));
                }
                return Unauthorized(Result<string>.Failure(new ApiError(ErrorCode.SYS_Unauthorized, $"Incorrect username or password!", 401)));
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
    }
}
