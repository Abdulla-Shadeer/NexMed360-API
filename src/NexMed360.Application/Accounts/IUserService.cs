using NexMed360Api.Common;
using NexMed360Api.Dtos.Overview;
using NexMed360Api.Dtos.User;
using NexMed360Api.Common.Auth;

namespace NexMed360Api.Interfaces.Services;

public interface IUserService
{
    public Task<Result<List<OverviewDto>>> GetOverview(CallerContext user);
    Task<Result<ViewUserDto>> GetUserAsync(string userId);
    Task<Result<ViewUserDto>> UpdateUser(UpdateUserDto updateDto, string? requesterUserId);
}
