using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class DbInitializerService(ApplicationDbContext context, RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager, ILogger<DbInitializerService> logger, IConfiguration configuration) : IDbInitializerService
{
    public async Task SeedData()
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        // These account roles keep the current screens working until phase 2.
        foreach (var name in new[] { UserRoles.PLATFORM_ADMIN, UserRoles.ORGANIZATION_MANAGER,
            UserRoles.ORGANIZATION_MEMBER, UserRoles.PRACTITIONER, UserRoles.PATIENT })
        {
            if (await roleManager.RoleExistsAsync(name)) continue;
            var result = await roleManager.CreateAsync(new IdentityRole(name));
            if (!result.Succeeded) throw new InvalidOperationException("Could not create the initial account roles.");
        }
        const string email = "platformadmin@example.test";
        var admin = await userManager.FindByEmailAsync(email);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true, IsPlatformAdmin = true,
                Person = new Person { FirstName = "Platform", LastName = "Administrator", Email = email }
            };
            var result = await userManager.CreateAsync(admin, configuration["Auth:PlatformAdminPassword"]!);
            if (!result.Succeeded) throw new InvalidOperationException("Could not create the platform administrator. Check the seed password.");
        }
        if (!await userManager.IsInRoleAsync(admin, UserRoles.PLATFORM_ADMIN))
        {
            var result = await userManager.AddToRoleAsync(admin, UserRoles.PLATFORM_ADMIN);
            if (!result.Succeeded) throw new InvalidOperationException("Could not grant platform administrator access.");
        }
        foreach (var (id, name, type) in new[]
        {
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), "Demo hospital", "Hospital"),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), "Demo clinic", "Clinic")
        })
        {
            if (!await context.Organizations.AnyAsync(row => row.Id == id))
                context.Organizations.Add(new Organization { Id = id, Name = name, Type = type });
            if (!await context.Locations.AnyAsync(row => row.OrganizationId == id))
                context.Locations.Add(new Location { OrganizationId = id, Name = "Main site", Type = LocationType.Site });
            foreach (var (roleName, grants) in new[]
            {
                ("Organization manager", new[] { Permissions.OrganizationsManage, Permissions.MembersManage, Permissions.RolesManage, Permissions.SchedulesManage }),
                ("Front office", new[] { Permissions.PatientsRead, Permissions.PatientsRegister, Permissions.AppointmentsManage }),
                ("Clinician", new[] { Permissions.PatientsRead, Permissions.AppointmentsManage, Permissions.SchedulesManage,
                    Permissions.EncountersStart, Permissions.ClinicalRecordsRead, Permissions.ClinicalRecordsWrite, Permissions.PrescriptionsIssue })
            })
            {
                if (!await context.AccessRoles.AnyAsync(row => row.OrganizationId == id && row.Name == roleName))
                    context.AccessRoles.Add(new AccessRole { OrganizationId = id, Name = roleName, Permissions = grants.ToList() });
            }
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Initial accounts and demo organizations are ready.");
    }
}
