using NexMed360Api.Dtos.User;
using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Accounts;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Repositories;

public sealed class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public async Task<ViewUserDto?> GetUserAsync(string userId)
    {
        var accounts =
            from account in context.Users.AsNoTracking()
            join person in context.People on account.PersonId equals person.Id
            where account.Id == userId && account.IsActive
            select new ViewUserDto
            {
                Id = account.Id, PersonId = person.Id.ToString(),
                FirstName = person.FirstName, LastName = person.LastName,
                Email = account.Email, PhoneNumber = account.PhoneNumber
            };

        return await accounts.FirstOrDefaultAsync();
    }

    public Task<bool> EmailExists(string email, string exceptUserId) =>
        context.Users.AnyAsync(user => user.Email == email && user.Id != exceptUserId);

    public async Task<ViewUserDto?> UpdateUser(UpdateUserDto updateDto)
    {
        var usr = await context.Users.FirstOrDefaultAsync(u => u.Id == updateDto.Id);
        if (usr == null || !usr.IsActive)
            return null;
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
