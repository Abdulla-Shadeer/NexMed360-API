using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Dtos.Overview;
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

        [HttpGet]
        [Route("GetOverview")]
        [Authorize]
        public async Task<ActionResult<Result<List<OverviewDto>>>> GetOverview()
        {
            try
            {
                var overview = await _userService.GetOverview(User);
                if (overview.IsSuccess)
                {
                    return Ok(overview);
                }
                return StatusCode(overview.Error!.HttpStatus, overview);
            }
            catch (Exception e)
            {
                return StatusCode(500, Result<List<OverviewDto>>.Failure(
                    new ApiError(ErrorCode.Internal, "An error occurred while processing the request!", 500, e.Message)
                    ));
            }
        }
    }
}
