using NexMed360Api.Dtos.User;
using NexMed360Api.Common;

namespace NexMed360.Application.Accounts.Internal;

// Credential operations stay behind the existing Identity service.
public sealed record AuthenticatedAccount(string UserId, string AccessToken);

public interface IIdentityGateway
{
    Task<Result<AuthenticatedAccount>> AuthenticateAsync(string email, string password);
    Task<ViewUserDto?> FindUserByEmail(string email);
    Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto user, string role);
}
