using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Overview;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinalProjectApi.Controllers;

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
        var loginResponse = await _authService.LoginAsync(userDto);
        if (loginResponse.IsSuccess)
        {
            return Ok(loginResponse);
        }
        return Unauthorized(loginResponse);
    }

    [HttpGet]
    [Route("GetUser")]
    [Authorize]
    public async Task<ActionResult<Result<ViewUserDto>>> GetUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var authResult = await _userService.GetUserAsync(userId);
        if (authResult.IsSuccess)
        {
            return Ok(authResult);
        }
        return Unauthorized(authResult);
    }

    [HttpGet]
    [Route("GetOverview")]
    [Authorize]
    public async Task<ActionResult<Result<List<OverviewDto>>>> GetOverview()
    {
        var overview = await _userService.GetOverview(User);
        if (overview.IsSuccess)
        {
            return Ok(overview);
        }
        return StatusCode(overview.Error!.HttpStatus, overview);
    }

    [HttpPost]
    [Route("UpdateUser")]
    [Authorize]
    public async Task<ActionResult<Result<ViewUserDto>>> UpdateUser(UpdateUserDto updateUserDto)
    {
        var updatedUser = await _userService.UpdateUser(updateUserDto);
        if (updatedUser.IsSuccess)
        {
            return Ok(updatedUser);
        }
        return StatusCode(updatedUser.Error!.HttpStatus, updatedUser);
    }
}
