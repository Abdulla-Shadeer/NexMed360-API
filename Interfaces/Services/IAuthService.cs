using FinalProjectApi.Common;
using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IAuthService
    {
        Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto);
    }
}
