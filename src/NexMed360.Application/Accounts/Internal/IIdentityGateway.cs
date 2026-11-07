using NexMed360Api.Common;
using NexMed360.Application.Shared;

namespace NexMed360.Application.Accounts.Internal;

// Infrastructure boundary used only by AuthService and UserService, not feature services/controllers.
public sealed record AccountRegistration(string? FirstName, string? LastName, string Email, string PhoneNumber, string Password);
public sealed record AccountChanges(string Id, string? FirstName, string? LastName, string? Email, string? PhoneNumber);
public sealed record AuthenticatedAccount(string UserId, string AccessToken);

public interface IIdentityGateway
{
    Task<Result<AuthenticatedAccount>> AuthenticateAsync(string email, string password);
    Task<Result<AccountSummary>> RegisterUser(AccountRegistration user, string role);
    Task<Result<AccountSummary>> GetUserAsync(string userId);
    Task<Result<AccountSummary>> UpdateUser(AccountChanges update, string? callerId);
}
