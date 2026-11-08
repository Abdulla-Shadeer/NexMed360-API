using NexMed360Api.Dtos.User;
using NexMed360.Application.Accounts.Internal;
using NexMed360Api.Common;

namespace NexMed360.Application.Accounts;

public interface IUserRepository
{
    Task<Result<ViewUserDto>> GetUserAsync(string userId);
    Task<Result<ViewUserDto>> UpdateUser(UpdateUserDto changes, string? callerId);
}
