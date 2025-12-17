using NexMed360Api.Common;
using NexMed360Api.Dtos.Overview;
using NexMed360Api.Dtos.User;
using System.Security.Claims;

namespace NexMed360Api.Interfaces.Services;

public interface IUserService
{
    public Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto user, string role);
    public Task<Result<List<OverviewDto>>> GetOverview(ClaimsPrincipal user);
    Task<Result<ViewUserDto>> GetUserAsync(string userId);
    Task<Result<ViewUserDto>> UpdateUser(UpdateUserDto updateDto);
}
