using FinalProjectApi.Common;
using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IUserService
    {
        public Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto user, string role);
    }
}
