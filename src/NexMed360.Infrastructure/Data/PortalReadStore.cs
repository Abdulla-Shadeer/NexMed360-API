using Microsoft.EntityFrameworkCore;
using NexMed360Api.Common.Auth;
using NexMed360.Application.Shared;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data;

public sealed class PortalReadStore(ApplicationDbContext context) : IPortalReadStore
{
    public async Task<PortalReadData> Load(PortalReadRequest request)
    {
        var profilePeople = await context.Users.Where(row => request.ProfileUserIds.Contains(row.Id)).Select(row => row.PersonId).ToListAsync();
        var patients = await context.Patients.Where(row => request.PatientIds.Contains(row.Id) || profilePeople.Contains(row.PersonId)).ToDictionaryAsync(row => row.Id);
        var slots = await context.SessionSlots.Where(row => request.SlotIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var sessions = await context.PractitionerSchedules.Where(row => request.SessionIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var practitionerIds = await context.Practitioners.Where(row => profilePeople.Contains(row.PersonId)).Select(row => row.Id).ToListAsync();
        var roleIds = request.PractitionerRoleIds.Concat(sessions.Values.Select(row => row.PractitionerRoleId)).Distinct().ToArray();
        var roles = await context.PractitionerRoles.Where(row => roleIds.Contains(row.Id) || practitionerIds.Contains(row.PractitionerId)).ToDictionaryAsync(row => row.Id);
        practitionerIds.AddRange(roles.Values.Select(row => row.PractitionerId));
        var practitioners = await context.Practitioners.Where(row => practitionerIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var memberships = await context.OrganizationMemberships.Where(row => request.ProfileUserIds.Contains(row.UserId)
            || (request.IncludeOrganizationManagers && request.OrganizationIds.Contains(row.OrganizationId)
                && context.MembershipAccessRoles.Any(link => link.MembershipId == row.Id
                    && context.AccessRoles.Any(role => role.Id == link.AccessRoleId && role.Permissions.Contains(Permissions.MembersManage))))).ToListAsync();
        var organizationIds = request.OrganizationIds.Concat(roles.Values.Select(row => row.OrganizationId))
            .Concat(memberships.Select(row => row.OrganizationId)).Distinct().ToArray();
        var organizations = await context.Organizations.Where(row => organizationIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var personIds = request.PersonIds.Concat(patients.Values.Select(row => row.PersonId)).Concat(practitioners.Values.Select(row => row.PersonId)).Concat(profilePeople).Distinct().ToArray();
        var accountIds = request.AccountIds.Concat(memberships.Select(row => row.UserId)).Distinct().ToArray();
        var users = await context.Users.Where(row => accountIds.Contains(row.Id) || personIds.Contains(row.PersonId)).ToListAsync();
        var accounts = users.ToDictionary(row => row.Id, row => new AccountSummary(row.Id, row.FirstName, row.LastName, row.Email, row.PhoneNumber) { PersonId = row.PersonId });
        var allPersonIds = personIds.Concat(users.Select(row => row.PersonId)).Distinct().ToArray();
        var people = await context.People.Where(row => allPersonIds.Contains(row.Id)).ToListAsync();
        var personAccounts = people.ToDictionary(row => row.Id, row => new AccountSummary("", row.FirstName, row.LastName, row.Email, row.PhoneNumber) { PersonId = row.Id });
        if (request.IncludeAccountRoles)
        {
            var ids = users.Select(row => row.Id).ToArray();
            var accountRoles = await context.UserRoles.Where(row => ids.Contains(row.UserId)).Join(context.Roles,
                row => row.RoleId, role => role.Id, (row, role) => new { row.UserId, role.Name }).ToListAsync();
            foreach (var role in accountRoles)
                if (role.Name != null) accounts[role.UserId].Roles.Add(role.Name);
        }
        foreach (var account in accounts.Values) personAccounts[account.PersonId] = account;
        var locationIds = memberships.Where(row => row.LocationId.HasValue).Select(row => row.LocationId!.Value)
            .Concat(roles.Values.Where(row => row.LocationId.HasValue).Select(row => row.LocationId!.Value)).Distinct().ToArray();
        var locations = await context.Locations.Where(row => locationIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        return new PortalReadData
        {
            Patients = patients,
            Practitioners = practitioners,
            PractitionerRoles = roles,
            Organizations = organizations,
            Sessions = sessions,
            Slots = slots,
            Accounts = accounts,
            People = personAccounts,
            Memberships = memberships,
            Locations = locations
        };
    }
}
