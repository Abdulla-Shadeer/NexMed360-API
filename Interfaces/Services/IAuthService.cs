using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IAuthService
    {
        Task<bool> ValidateUser(LoginUserDto userDto);
        Task<String> GenerateToken();
    }
}
