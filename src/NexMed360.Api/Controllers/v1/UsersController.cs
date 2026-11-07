using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Dtos.Overview;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NexMed360Api.Controllers.v1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
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
        var overview = await _userService.GetOverview(User.ToCallerContext());
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
        var requesterUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var updatedUser = await _userService.UpdateUser(updateUserDto, requesterUserId);
        if (updatedUser.IsSuccess)
        {
            return Ok(updatedUser);
        }
        return StatusCode(updatedUser.Error!.HttpStatus, updatedUser);
    }
}
