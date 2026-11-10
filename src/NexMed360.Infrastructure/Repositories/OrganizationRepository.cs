using NexMed360Api.Dtos.Organization;
using NexMed360Api.Dtos.User;
using Microsoft.EntityFrameworkCore;
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
    public Task<bool> HasPermission(Guid id, string userId, string permission)
    {
        var membersWithPermission =
            from member in context.OrganizationMembers
            join assignment in context.MemberRoleAssignments
                on new { OrganizationMemberId = member.Id, member.OrganizationId }
                equals new { assignment.OrganizationMemberId, assignment.OrganizationId }
            join role in context.AccessRoles
                on new { Id = assignment.AccessRoleId, assignment.OrganizationId }
                equals new { role.Id, role.OrganizationId }
            where member.OrganizationId == id && member.UserId == userId && member.IsActive
                && role.Permissions.Contains(permission)
            select member.Id;

        return membersWithPermission.AnyAsync();
    }

    public Task<ViewOrganizationDto?> View(Organization? row) => Task.FromResult(row == null ? null : Map(row));
    public Task<List<ViewOrganizationDto>> Views(IEnumerable<Organization> rows) => Task.FromResult(rows.Select(Map).ToList());
    private static ViewOrganizationDto Map(Organization row) => new()
    {
        Id = row.Id.ToString(), Name = row.Name, Type = row.Type, Street = row.Street, City = row.City,
        District = row.District, Phone = row.Phone, Grade = row.Grade
    };
    public Task<List<ViewUserDto>> MemberViews(Guid organizationId)
    {
        var members =
            from member in context.OrganizationMembers
            join user in context.Users on member.UserId equals user.Id
            join person in context.People on user.PersonId equals person.Id
            where member.OrganizationId == organizationId
            select new ViewUserDto
            {
                Id = user.Id, PersonId = person.Id.ToString(), FirstName = person.FirstName, LastName = person.LastName,
                Email = user.Email, PhoneNumber = user.PhoneNumber
            };

        return members.ToListAsync();
    }

}
