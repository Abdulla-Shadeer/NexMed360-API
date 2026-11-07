using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.API.Old.Tests;

// Shared synthetic identities keep the existing security fixtures readable.
internal static class TestProfiles
{
    private static readonly ConcurrentDictionary<Guid, string> Accounts = new();

    internal static Guid PersonId(string userId) => Key("person:", userId);
    internal static Guid PractitionerId(string userId) => Key("practitioner:", userId);
    internal static string UserId(Patient patient) => Accounts[patient.PersonId];
    internal static string UserId(PractitionerRole role) => Accounts[role.PractitionerId];

    private static Guid Key(string kind, string userId)
    {
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(kind + userId)).AsSpan(0, 16));
        Accounts[id] = userId;
        return id;
    }

    internal static int Save(ApplicationDbContext context, bool acceptAllChangesOnSuccess = true)
    {
        Complete(context);
        return context.SaveChanges(acceptAllChangesOnSuccess);
    }

    internal static Task<int> SaveAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        Complete(context);
        return context.SaveChangesAsync(cancellationToken);
    }

    private static void Complete(ApplicationDbContext context)
    {
        foreach (var user in context.Users.Local.ToArray())
        {
            if (context.Entry(user).State != EntityState.Added) continue;
            user.Person.Id = PersonId(user.Id);
            user.PersonId = user.Person.Id;
            user.Person.Email = user.Email;
            user.Person.PhoneNumber = user.PhoneNumber;
        }
        foreach (var role in context.PractitionerRoles.Local.ToArray())
        {
            if (!Accounts.TryGetValue(role.PractitionerId, out var userId)) continue;
            if (context.Practitioners.Local.Any(row => row.Id == role.PractitionerId) || context.Practitioners.Any(row => row.Id == role.PractitionerId)) continue;
            var personId = PersonId(userId);
            if (!context.People.Local.Any(row => row.Id == personId) && !context.People.Any(row => row.Id == personId))
                context.People.Add(new Person { Id = personId, FirstName = "Synthetic", LastName = "Practitioner" });
            context.Practitioners.Add(new Practitioner { Id = role.PractitionerId, PersonId = personId });
        }
        foreach (var patient in context.Patients.Local.ToArray())
        {
            if (context.People.Local.Any(row => row.Id == patient.PersonId) || context.People.Any(row => row.Id == patient.PersonId)) continue;
            context.People.Add(new Person { Id = patient.PersonId, FirstName = "Synthetic", LastName = "Patient" });
        }
        foreach (var assignment in context.PractitionerRoles.Local.ToArray())
        {
            if (!Accounts.TryGetValue(assignment.PractitionerId, out var userId)) continue;
            if (context.OrganizationMemberships.Local.Any(row => row.OrganizationId == assignment.OrganizationId && row.UserId == userId)
                || context.OrganizationMemberships.Any(row => row.OrganizationId == assignment.OrganizationId && row.UserId == userId)) continue;
            context.Add(new OrganizationMembership { Id = Key("membership:" + assignment.OrganizationId, userId), OrganizationId = assignment.OrganizationId, UserId = userId });
        }
        foreach (var membership in context.OrganizationMemberships.Local.ToArray())
        {
            var managerRoleIds = context.Roles.Local.Where(row => row.Name == NexMed360Api.Common.Auth.UserRoles.ORGANIZATION_MANAGER).Select(row => row.Id).ToArray();
            if (!context.UserRoles.Local.Any(row => row.UserId == membership.UserId && managerRoleIds.Contains(row.RoleId))) continue;
            var role = context.AccessRoles.Local.FirstOrDefault(row => row.OrganizationId == membership.OrganizationId && row.Name == "Test manager");
            if (role == null)
            {
                role = new AccessRole { OrganizationId = membership.OrganizationId, Name = "Test manager", Permissions = [NexMed360Api.Common.Auth.Permissions.MembersManage] };
                context.Add(role);
            }
            if (!context.MembershipAccessRoles.Local.Any(row => row.MembershipId == membership.Id && row.AccessRoleId == role.Id)
                && !context.MembershipAccessRoles.Any(row => row.MembershipId == membership.Id && row.AccessRoleId == role.Id))
                context.Add(new MembershipAccessRole { OrganizationId = membership.OrganizationId, MembershipId = membership.Id, AccessRoleId = role.Id });
        }
        foreach (var slot in context.SessionSlots.Local)
            slot.OrganizationId = slot.Session?.OrganizationId ?? context.PractitionerSchedules.Local.First(row => row.Id == slot.SessionId).OrganizationId;
        foreach (var history in context.SessionHistories.Local)
            history.OrganizationId = history.PractitionerSchedule?.OrganizationId ?? context.PractitionerSchedules.Local.First(row => row.Id == history.PractitionerScheduleId).OrganizationId;
        foreach (var record in context.ClinicalRecords.Local)
        {
            if (record.OrganizationId == Guid.Empty)
                record.OrganizationId = context.PractitionerRoles.Local.FirstOrDefault(row => row.Id == record.PractitionerRoleId)?.OrganizationId
                    ?? context.PractitionerRoles.First(row => row.Id == record.PractitionerRoleId).OrganizationId;
            foreach (var prescription in record.Prescriptions ?? []) prescription.OrganizationId = record.OrganizationId;
        }
        foreach (var request in context.CancellationRequests.Local)
            request.OrganizationId = request.Appointment?.OrganizationId ?? context.Appointments.Local.First(row => row.Id == request.AppointmentId).OrganizationId;
    }
}
