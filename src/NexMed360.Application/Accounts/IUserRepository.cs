using NexMed360Api.Dtos.User;

namespace NexMed360.Application.Accounts;

public interface IUserRepository
{
    Task<ViewUserDto?> GetUserAsync(string userId);
    Task<bool> EmailExists(string email, string exceptUserId);
    Task<ViewUserDto?> UpdateUser(UpdateUserDto changes);
}
