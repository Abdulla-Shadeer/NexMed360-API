using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IUserService
    {
        public Task<RegisterUserDto> RegisterUser(RegisterUserDto user);
    }
}
