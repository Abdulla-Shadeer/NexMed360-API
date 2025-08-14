using FinalProjectApi.Common;
using FinalProjectApi.Dtos.User;
using System.Security.Claims;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IAuthService
    {
        Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto);
        Task<Result<ViewUserDto>> GetUserAsync(ClaimsPrincipal user);
    }
}
