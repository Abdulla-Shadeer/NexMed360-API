using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Overview;
using FinalProjectApi.Dtos.User;
using System.Security.Claims;

namespace FinalProjectApi.Interfaces.Services;

public interface IUserService
{
    public Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto user, string role);
    public Task<Result<List<OverviewDto>>> GetOverview(ClaimsPrincipal user);
    Task<Result<ViewUserDto>> GetUserAsync(string userId);
}
