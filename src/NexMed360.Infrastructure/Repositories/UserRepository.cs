using NexMed360Api.Dtos.User;
using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Accounts;
using NexMed360.Application.Accounts.Internal;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360.Domain.Rules;

namespace NexMed360.Infrastructure.Repositories;

public sealed class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public async Task<Result<ViewUserDto>> GetUserAsync(string userId)
    {
        var user = await context.Users.AsNoTracking().Where(row => row.Id == userId && row.IsActive)
            .Select(row => new ViewUserDto { Id = row.Id, FirstName = row.Person.FirstName, LastName = row.Person.LastName, Email = row.Email, PhoneNumber = row.PhoneNumber, PersonId = row.PersonId.ToString() }).FirstOrDefaultAsync();
        if (user == null) return ApiError.From(ErrorCode.SYS_Unauthorized, $"Account with ID {userId} not found.");
        return user;
    }
    public async Task<Result<ViewUserDto>> UpdateUser(UpdateUserDto updateDto, string? requesterUserId)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
            return ErrorCode.SYS_Unauthorized;

        if (!AccountRules.IsSelf(updateDto.Id, requesterUserId))
            return ApiError.From(ErrorCode.SYS_Unauthorized,
                "You can update only your own account.", 403);

        if (updateDto.Email != null &&
            await context.Users.AnyAsync(u => u.Email == updateDto.Email && u.Id != requesterUserId))
        {
            return ApiError.From(ErrorCode.SYS_DuplicateEntry, $"This email '{updateDto.Email}' is already used by someone.");
        }

        var usr = await context.Users.FirstOrDefaultAsync(u => u.Id == requesterUserId);
        if (usr == null || !usr.IsActive)
            return ErrorCode.USR_NotFound;
        usr.FirstName = updateDto.FirstName ?? usr.FirstName;
        usr.LastName = updateDto.LastName ?? usr.LastName;
        if (updateDto.Email != null)
        {
            usr.Email = updateDto.Email;
            usr.UserName = updateDto.Email;
            usr.NormalizedEmail = updateDto.Email.ToUpperInvariant();
            usr.NormalizedUserName = updateDto.Email.ToUpperInvariant();
        }
        usr.PhoneNumber = updateDto.PhoneNumber ?? usr.PhoneNumber;
        usr.Person.Email = usr.Email;
        usr.Person.PhoneNumber = usr.PhoneNumber;

        await context.SaveChangesAsync();
        return Summary(usr);
    }
    private static ViewUserDto Summary(ApplicationUser user)
        => new() { Id = user.Id, FirstName = user.FirstName, LastName = user.LastName,
            Email = user.Email, PhoneNumber = user.PhoneNumber, PersonId = user.PersonId.ToString() };

}
