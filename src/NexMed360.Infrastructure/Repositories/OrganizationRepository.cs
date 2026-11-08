using NexMed360Api.Dtos.Organization;
using NexMed360Api.Dtos.User;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure.Repositories;

public class OrganizationRepository(ApplicationDbContext context) : IOrganizationRepository
{
    public Task<bool> Exists(Guid id) => context.Organizations.AnyAsync(row => row.Id == id);
    public Task<Organization?> Find(Guid id) => context.Organizations.FirstOrDefaultAsync(row => row.Id == id);
    public async Task<Organization> Add(Organization row) { context.Organizations.Add(row); await context.SaveChangesAsync(); return row; }
    public async Task<Organization> Save(Organization row) { await context.SaveChangesAsync(); return row; }
    public Task<List<Organization>> GetAll() => context.Organizations.ToListAsync();
    public Task<bool> IsAdmin(Guid id, string userId) => context.OrganizationMemberships.AnyAsync(member => member.OrganizationId == id && member.UserId == userId && member.IsActive
        && context.MembershipAccessRoles.Any(link => link.MembershipId == member.Id && context.AccessRoles.Any(role => role.Id == link.AccessRoleId && role.Permissions.Contains(Permissions.MembersManage))));

    public Task<ViewOrganizationDto?> View(Organization? row) => Task.FromResult(row == null ? null : Map(row));
    public Task<List<ViewOrganizationDto>> Views(IEnumerable<Organization> rows) => Task.FromResult(rows.Select(Map).ToList());
    private static ViewOrganizationDto Map(Organization row) => new()
    {
        Id = row.Id.ToString(), Name = row.Name, Type = row.Type, Street = row.Street, City = row.City,
        District = row.District, Phone = row.Phone, Grade = row.Grade
    };
    public Task<List<ViewUserDto>> MemberViews(Guid organizationId) =>
        (from member in context.OrganizationMemberships join user in context.Users on member.UserId equals user.Id
         where member.OrganizationId == organizationId select new ViewUserDto
         {
             Id = user.Id, PersonId = user.PersonId.ToString(), FirstName = user.Person.FirstName, LastName = user.Person.LastName,
             Email = user.Email, PhoneNumber = user.PhoneNumber
         }).ToListAsync();

}
