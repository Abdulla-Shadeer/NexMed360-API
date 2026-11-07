using NexMed360.Application.Accounts.Internal;
using NexMed360.Application.Shared;
using NexMed360Api.Common;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class AuthService(IIdentityGateway identity, IUserService users, PortalViews views) : IAuthService
{
    public async Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto dto, string role)
    {
        var result = await identity.RegisterUser(new AccountRegistration(dto.FirstName, dto.LastName, dto.Email, dto.PhoneNumber, dto.Password), role);
        return result.IsSuccess ? Result<ViewUserDto>.Success(await views.User(result.Data!, result.Data!.IncludeProfiles)) : Result<ViewUserDto>.Failure(result.Error!);
    }
    public async Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto dto)
    {
        var account = await identity.AuthenticateAsync(dto.Email, dto.Password);
        if (!account.IsSuccess) return Result<LoginResponseDto>.Failure(account.Error!);
        var user = await users.GetUserAsync(account.Data!.UserId);
        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = account.Data.AccessToken,
            User = user.Data!,
        });
    }
}
