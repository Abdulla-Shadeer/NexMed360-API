using NexMed360Api.Common;
using NexMed360Api.Dtos.User;

namespace NexMed360Api.Interfaces.Services
{
    public interface IAuthService
    {
        Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto user, string role);
        Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto);
    }
}
