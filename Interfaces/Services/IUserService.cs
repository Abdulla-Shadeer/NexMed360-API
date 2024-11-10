using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IUserService
    {
        public Task<string> RegisterUser(RegisterUserDto user);
    }
}
