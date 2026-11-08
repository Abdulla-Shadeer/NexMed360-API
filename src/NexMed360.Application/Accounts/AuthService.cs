using NexMed360.Application.Accounts.Internal;
using NexMed360Api.Common;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class AuthService(IIdentityGateway identity, IUserService users) : IAuthService
{
    public Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto dto, string role) => identity.RegisterUser(dto, role);

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto dto)
    {
        var account = await identity.AuthenticateAsync(dto.Email, dto.Password);
        if (!account.IsSuccess) return account.Error!;

        var user = await users.GetUserAsync(account.Data!.UserId);
        if (!user.IsSuccess) return user.Error!;

        return new LoginResponseDto
        {
            AccessToken = account.Data.AccessToken,
            User = user.Data!,
        };
    }
}
