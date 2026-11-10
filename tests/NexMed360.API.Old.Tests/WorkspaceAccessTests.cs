using NexMed360Api.Dtos.User;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Access;
using NexMed360.Infrastructure.Repositories;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Controllers.v1;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Entities;
using NexMed360Api.Services;

namespace NexMed360.API.Old.Tests;

public class WorkspaceAccessTests
{
    [Theory]
    [InlineData("Get")]
    [InlineData("PermissionList")]
    [InlineData("Roles")]
    [InlineData("Members")]
    [InlineData("MemberRoles")]
    [InlineData("Locations")]
    public async Task WorkspaceReadsReturnTheResultEnvelopeExpectedByTheUi(string endpoint)
    {
        using var f = new Fixture();
        if (endpoint != "Get" && endpoint != "PermissionList") await f.Select(f.A);
        var controller = new WorkspaceController(f.Service, f.Workspaces, f.Access)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "staff")], "test"))
                }
            }
        };
        var action = endpoint switch
        {
            "Get" => (await controller.Get()).Result,
            "PermissionList" => controller.PermissionList().Result,
            "Roles" => (await controller.Roles()).Result,
            "Members" => (await controller.Members()).Result,
            "MemberRoles" => (await controller.MemberRoles()).Result,
            "Locations" => (await controller.Locations()).Result,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
        var response = Assert.IsType<OkObjectResult>(action);
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(
            response.Value, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        Assert.Equal(System.Text.Json.JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.True(json.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("error").ValueKind);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(System.Text.Json.JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() > 0);
        if (endpoint == "Get") Assert.Contains(data.EnumerateArray(), row => row.GetProperty("key").GetString() == WorkspaceModes.Patient);
    }

    [Fact]
    public async Task WorkspaceListIsSmallAndCurrentDetailsComeOnlyFromTheSelectedScope()
    {
        using var f = new Fixture();
        var options = await f.Workspaces.List("staff");
        Assert.Contains(options, row => row.Key == WorkspaceModes.Patient);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(options[0], new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(new[] { "key", "mode", "name" }, json.EnumerateObject().Select(row => row.Name).Order());
        await f.Select(f.A, f.AssignmentA);
        var controller = new WorkspaceController(f.Service, f.Workspaces, f.Access);
        var response = Assert.IsType<OkObjectResult>(controller.Current().Result);
        var current = Assert.IsType<Result<WorkspaceDto>>(response.Value).Data!;
        Assert.Equal(f.A.Id, current.OrganizationId);
        Assert.Equal(f.A.Name, current.OrganizationName);
        Assert.Equal(f.AssignmentA.Id, current.PractitionerRoleId);
        Assert.Contains(Permissions.ClinicalRecordsRead, current.Permissions);
        Assert.Null(await f.Workspaces.Resolve("other", current.Key));
        Assert.Null(await f.Workspaces.Resolve("staff", "work:invalid"));
        Assert.Null(await f.Workspaces.Resolve("staff", $"{WorkspaceModes.Work}:{f.A.Id}:{f.AssignmentB.Id}"));
    }

    [Fact]
    public async Task OwnPatientDetailsUseThePatientInTheTrustedWorkspace()
    {
        using var f = new Fixture();
        var service = new PatientService(null!, new PatientRepository(f.Context), null!, new ClinicalRecordRepository(f.Context), f.Access);
        Assert.False((await service.GetOwnPatient()).IsSuccess);
        f.Access.Select((await f.Workspaces.Resolve("staff", WorkspaceModes.Patient))!);
        var response = await service.GetOwnPatient();
        Assert.True(response.IsSuccess);
        Assert.Equal(f.Own.Id.ToString(), response.Data!.Id);
        Assert.Equal(f.Own.PersonId.ToString(), response.Data.PersonId);
    }

    [Fact]
    public async Task OneAccountHasPatientModeAndDifferentGrantsAtTwoOrganizations()
    {
        using var f = new Fixture();
        var choices = await f.Scopes("staff");
        Assert.Contains(choices, row => row.Key == WorkspaceModes.Patient && row.PatientId == f.Own.Id);
        Assert.Contains(choices, row => row.OrganizationId == f.A.Id && row.Permissions.Contains(Permissions.ClinicalRecordsRead));
        Assert.Contains(choices, row => row.OrganizationId == f.B.Id && !row.Permissions.Contains(Permissions.ClinicalRecordsRead));
        Assert.Contains(choices, row => row.PractitionerRoleId == f.AssignmentA.Id);
        Assert.Contains(choices, row => row.PractitionerRoleId == f.AssignmentB.Id);
    }

    [Fact]
    public async Task StaffQueriesHideOtherOrganizationsAndUnregisteredPatients()
    {
        using var f = new Fixture();
        await f.Select(f.A);
        Assert.All(await f.Context.Appointments.ToListAsync(), row => Assert.Equal(f.A.Id, row.OrganizationId));
        Assert.All(await f.Context.ClinicalRecords.ToListAsync(), row => Assert.Equal(f.A.Id, row.OrganizationId));
        Assert.All(await f.Context.PractitionerSchedules.ToListAsync(), row => Assert.Equal(f.A.Id, row.OrganizationId));
        Assert.All(await f.Context.AccessRoles.ToListAsync(), row => Assert.Equal(f.A.Id, row.OrganizationId));
        Assert.DoesNotContain(await f.Context.Patients.ToListAsync(), row => row.Id == f.Unregistered.Id);
        Assert.Null(await new ClinicalRecordRepository(f.Context).FindReadable(f.RecordB.Id, "staff", true));
        Assert.Null(await new AppointmentRepository(f.Context).FindReadable(f.AppointmentB.Id, "staff", [Permissions.AppointmentsManage]));
    }

    [Fact]
    public async Task StaffWithoutClinicalPermissionCannotReadRecordsEvenWithAnAssignment()
    {
        using var f = new Fixture();
        await f.Select(f.B, f.AssignmentB);
        Assert.Empty(await f.Context.ClinicalRecords.ToListAsync());
        Assert.Empty(await f.Context.Prescriptions.ToListAsync());
    }

    [Fact]
    public async Task PatientHistoryUsesTheAccountIdentityAcrossOrganizations()
    {
        using var f = new Fixture();
        f.Access.Select((await f.Scopes("staff")).Single(row => row.Key == WorkspaceModes.Patient));
        var records = await f.Context.ClinicalRecords.Include(row => row.Prescriptions).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Contains(records, row => row.OrganizationId == f.A.Id);
        Assert.Contains(records, row => row.OrganizationId == f.B.Id);
        Assert.All(records, row => Assert.Equal(f.Own.Id, row.PatientId));
        Assert.DoesNotContain(records, row => !row.IsPatientVisible);
        Assert.Null(await new ClinicalRecordRepository(f.Context).FindReadable(f.OtherRecord.Id, "staff", true));
        Assert.False(await new PatientRepository(f.Context).CanReadPatient(f.Other.Id, "staff", false));
    }

    [Fact]
    public async Task PatientCannotBookForAnotherPatient()
    {
        using var f = new Fixture();
        f.Access.Select((await f.Scopes("staff")).Single(row => row.Key == WorkspaceModes.Patient));
        var service = new AppointmentService(new AppointmentRepository(f.Context), f.Access);
        var result = await service.Create(new CreateAppointmentDto { PatientId = f.Other.Id, SessionSlotId = f.SlotA.Id }, "staff");
        Assert.Equal(403, result.Error!.HttpStatus);
        Assert.Equal(3, await f.Context.Appointments.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task ClinicalWritesRequireTheSelectedAssignmentAndPrescriptionGrant()
    {
        using var f = new Fixture();
        await f.Select(f.A, f.AssignmentA);
        f.Access.Current!.Permissions.Remove(Permissions.PrescriptionsIssue);
        var service = new PatientService(null!, new PatientRepository(f.Context), null!, new ClinicalRecordRepository(f.Context), f.Access);
        var result = await service.AddToClinicalRecords(new AddClinicalRecordDto
        {
            PatientId = f.Own.Id.ToString(), PractitionerRoleId = f.AssignmentA.Id, Diagnosis = "Test",
            Prescriptions = [new() { MedicineName = "Test", Dosage = "Test", TimesPerDay = 1 }]
        }, "staff");
        Assert.Equal(403, result.Error!.HttpStatus);
        var wrongAssignment = await service.AddToClinicalRecords(new AddClinicalRecordDto
        {
            PatientId = f.Own.Id.ToString(), PractitionerRoleId = f.AssignmentB.Id, Diagnosis = "Test"
        }, "staff");
        Assert.Equal(403, wrongAssignment.Error!.HttpStatus);
    }

    [Fact]
    public async Task ClinicalWritesStoreTheSelectedOrganizationAndAssignment()
    {
        using var f = new Fixture();
        await f.Select(f.A, f.AssignmentA);
        var service = new PatientService(null!, new PatientRepository(f.Context), null!, new ClinicalRecordRepository(f.Context), f.Access);
        var result = await service.AddToClinicalRecords(new AddClinicalRecordDto
        {
            PatientId = f.Own.Id.ToString(), PractitionerRoleId = f.AssignmentA.Id, Diagnosis = "New synthetic record"
        }, "staff");
        Assert.True(result.IsSuccess);
        var saved = await f.Context.ClinicalRecords.SingleAsync(row => row.Id.ToString() == result.Data!.Id);
        Assert.Equal(f.A.Id, saved.OrganizationId);
        Assert.Equal(f.AssignmentA.Id, saved.PractitionerRoleId);
    }

    [Fact]
    public async Task CrossOrganizationWritesFailBeforeSaving()
    {
        using var f = new Fixture();
        await f.Select(f.A);
        f.Context.AccessRoles.Add(new AccessRole { OrganizationId = f.B.Id, Name = "Wrong organization" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task CustomRolesCanBeCreatedChangedAndAssignedWithinTheOrganization()
    {
        using var f = new Fixture();
        await f.Select(f.A);
        var created = await f.Service.SaveRole(null, new("Records assistant", [Permissions.PatientsRead]));
        Assert.True(created.IsSuccess);
        var changed = await f.Service.SaveRole(created.Data!.Id, new("Patient assistant", [Permissions.PatientsRead, Permissions.PatientsRegister]));
        Assert.True(changed.IsSuccess);
        var updated = await f.Service.UpdateMember(f.MemberA.Id, new(true, [created.Data.Id], null));
        Assert.True(updated.IsSuccess);
        var choices = await f.Scopes("staff");
        var fresh = choices.Single(row => row.Key == $"{WorkspaceModes.Work}:{f.A.Id}");
        Assert.Equal(new[] { Permissions.PatientsRead, Permissions.PatientsRegister }.Order(), fresh.Permissions.Order());
    }

    [Fact]
    public async Task ReplacingAndClearingMemberRolesKeepsRoleDefinitionsAndOtherOrganizations()
    {
        using var f = new Fixture();
        await f.Select(f.A);
        var created = await f.Service.SaveRole(null, new("Records assistant", [Permissions.PatientsRead]));
        var roleId = created.Data!.Id;

        // Reload between updates so saved relationships, rather than tracked objects, are checked.
        foreach (var roleIds in new Guid[][] { [f.RoleA.Id, roleId], [roleId], [] })
        {
            f.Context.ChangeTracker.Clear();
            var updated = await f.Service.UpdateMember(f.MemberA.Id, new(true, roleIds.ToList(), null));
            Assert.True(updated.IsSuccess);
            f.Context.ChangeTracker.Clear();

            var member = Assert.Single(await f.Service.Members());
            Assert.Equal(roleIds.Order(), member.AccessRoleIds.Order());
        }

        Assert.Equal(2, await f.Context.AccessRoles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty((await f.Workspaces.Resolve("staff", $"{WorkspaceModes.Work}:{f.A.Id}"))!.Permissions);
        var otherWorkspace = await f.Workspaces.Resolve("staff", $"{WorkspaceModes.Work}:{f.B.Id}");
        Assert.Equal(new[] { Permissions.PatientsRead }, otherWorkspace!.Permissions);
    }

    [Fact]
    public async Task RoleAdministratorsCanAssignClinicalGrantsWithoutReceivingThem()
    {
        using var f = new Fixture();
        f.RoleA.Permissions = [Permissions.RolesManage, Permissions.MembersManage];
        f.Context.AccessRoles.Update(f.RoleA);
        await f.Context.SaveChangesAsync();
        f.Context.ChangeTracker.Clear();
        await f.Select(f.A);
        var created = await f.Service.SaveRole(null, new("Prescriber", [Permissions.ClinicalRecordsRead, Permissions.PrescriptionsIssue]));
        Assert.True(created.IsSuccess);
        var updated = await f.Service.SaveRole(created.Data!.Id, new("Clinician", [Permissions.ClinicalRecordsRead, Permissions.EncountersStart]));
        Assert.True(updated.IsSuccess);
        var member = new OrganizationMembership { OrganizationId = f.A.Id, UserId = "other" };
        f.Context.OrganizationMemberships.Add(member);
        await f.Context.SaveChangesAsync();
        Assert.True((await f.Service.UpdateMember(member.Id, new(true, [created.Data.Id], null))).IsSuccess);
        var staffAccess = (await f.Scopes("other")).Single(row => row.Key == $"{WorkspaceModes.Work}:{f.A.Id}");
        Assert.Contains(Permissions.ClinicalRecordsRead, staffAccess.Permissions);
        var adminAccess = (await f.Scopes("staff")).Single(row => row.Key == $"{WorkspaceModes.Work}:{f.A.Id}");
        Assert.DoesNotContain(Permissions.ClinicalRecordsRead, adminAccess.Permissions);
        Assert.Empty(await f.Context.ClinicalRecords.ToListAsync());
        Assert.False((await f.Service.SaveRole(null, new("Invalid platform grant", ["platform.admin"]))).IsSuccess);
        Assert.False((await f.Service.UpdateMember(member.Id, new(true, [f.RoleB.Id], null))).IsSuccess);
    }

    [Fact]
    public async Task OrganizationAdministratorCanAddAPractitionerWithoutClinicalGrants()
    {
        using var f = new Fixture();
        f.RoleA.Permissions = [Permissions.MembersManage, Permissions.RolesManage, Permissions.OrganizationsManage];
        f.Context.AccessRoles.Update(f.RoleA);
        await f.Context.SaveChangesAsync();
        f.Context.ChangeTracker.Clear();
        await f.Select(f.A);
        var account = new NexMed360Api.Dtos.User.ViewUserDto { Id = "other", PersonId = f.Other.PersonId.ToString(), Email = "other@example.test" };
        var service = new OrganizationService(new OrganizationRepository(f.Context), new ExistingAccount(account),
            new NoopRegistrationTransaction(), new PractitionerRoleRepository(f.Context),
            new OrganizationMembershipRepository(f.Context), f.Access);
        var result = await service.AddPractitionerRole(new NexMed360Api.Dtos.PractitionerRole.CreatePractitionerRoleDto
        {
            OrganizationId = f.A.Id.ToString(), RoleName = "Physician", Specialty = "General medicine",
            PersonalInformation = new() { FirstName = "Other", LastName = "Patient", Email = "other@example.test", Password = "Synthetic-password1!" }
        });
        Assert.True(result.IsSuccess);
        Assert.NotNull(await f.Context.PractitionerRoles.FirstOrDefaultAsync(row => row.OrganizationId == f.A.Id && f.Context.Practitioners.Any(person => person.Id == row.PractitionerId && person.PersonId == Guid.Parse(account.PersonId))));
        Assert.Contains((await f.Scopes("other")).Single(row => row.Key == $"{WorkspaceModes.Work}:{f.A.Id}").Permissions, permission => permission == Permissions.ClinicalRecordsRead);
        Assert.DoesNotContain((await f.Scopes("staff")).Single(row => row.Key == $"{WorkspaceModes.Work}:{f.A.Id}").Permissions, permission => permission == Permissions.ClinicalRecordsRead);
    }

    [Fact]
    public async Task ClinicalPermissionsDoNotAllowManagingStaffOrRoles()
    {
        using var f = new Fixture();
        await f.Select(f.A);
        f.Access.Current!.Permissions = [Permissions.ClinicalRecordsRead, Permissions.PrescriptionsIssue];
        Assert.Equal(403, (await f.Service.SaveRole(null, new("Staff", [Permissions.PatientsRead]))).Error!.HttpStatus);
        Assert.Equal(403, (await f.Service.UpdateMember(f.MemberA.Id, new(true, [f.RoleA.Id], null))).Error!.HttpStatus);
    }

    [Fact]
    public async Task PlatformCanViewUpdateAndRemoveAdministratorAccessWithoutDeletingTheAccount()
    {
        using var f = new Fixture();
        f.Access.Select(new() { Key = WorkspaceModes.Platform, Mode = WorkspaceModes.Platform });
        var admins = await f.Service.OrganizationAdministrators(f.A.Id);
        Assert.True(admins.IsSuccess);
        Assert.Equal("staff@example.test", Assert.Single(admins.Data!.Administrators).Email);
        var changed = await f.Service.UpdateAdministrator(f.A.Id, f.MemberA.Id, new(false, Permissions.Administration.ToList()));
        Assert.True(changed.IsSuccess);
        var admin = Assert.Single((await f.Service.OrganizationAdministrators(f.A.Id)).Data!.Administrators);
        Assert.False(admin.IsActive);
        Assert.DoesNotContain(Permissions.ClinicalRecordsRead, admin.Permissions);
        Assert.DoesNotContain(await f.Scopes("staff"), row => row.OrganizationId == f.A.Id);
        Assert.True((await f.Service.RemoveAdministrator(f.A.Id, f.MemberA.Id)).IsSuccess);
        f.Context.ChangeTracker.Clear();
        Assert.Empty((await f.Service.OrganizationAdministrators(f.A.Id)).Data!.Administrators);
        Assert.False(await f.Context.MembershipAccessRoles.AnyAsync(link => link.MembershipId == f.MemberA.Id));
        Assert.NotNull(await f.Context.AccessRoles.FindAsync(f.RoleA.Id));
        Assert.NotNull(await f.Context.Users.FindAsync("staff"));
        Assert.Contains(await f.Scopes("staff"), row => row.OrganizationId == f.B.Id);
        Assert.Contains(await f.Scopes("staff"), row => row.Mode == WorkspaceModes.Patient);
        Assert.NotNull(await f.Context.ClinicalRecords.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.PatientId == f.Own.Id));
    }

    [Fact]
    public async Task UpdatingOneAdministratorDoesNotChangeOtherMembersSharingTheirRole()
    {
        using var f = new Fixture();
        var otherMember = new OrganizationMembership { UserId = "other", OrganizationId = f.A.Id };
        f.Context.AddRange(otherMember, new MembershipAccessRole { OrganizationId = f.A.Id, MembershipId = otherMember.Id, AccessRoleId = f.RoleA.Id });
        await f.Context.SaveChangesAsync();
        f.Access.Select(new() { Key = WorkspaceModes.Platform, Mode = WorkspaceModes.Platform });
        Assert.True((await f.Service.UpdateAdministrator(f.A.Id, f.MemberA.Id, new(true, Permissions.Administration.ToList()))).IsSuccess);
        var other = (await f.Scopes("other")).Single(row => row.Key == $"{WorkspaceModes.Work}:{f.A.Id}");
        Assert.Contains(Permissions.ClinicalRecordsRead, other.Permissions);
        var admin = (await f.Scopes("staff")).Single(row => row.Key == $"{WorkspaceModes.Work}:{f.A.Id}");
        Assert.DoesNotContain(Permissions.ClinicalRecordsRead, admin.Permissions);
    }

    [Fact]
    public async Task AdministratorRoleCloningUsesAnUnusedNameAndReusesItsPrivateRoleOnLaterEdits()
    {
        using var f = new Fixture();
        var otherMember = new OrganizationMembership { UserId = "other", OrganizationId = f.A.Id };
        f.Context.AddRange(otherMember,
            new MembershipAccessRole { OrganizationId = f.A.Id, MembershipId = otherMember.Id, AccessRoleId = f.RoleA.Id },
            new AccessRole { OrganizationId = f.A.Id, Name = "Administrator - Staff Patient" });
        await f.Context.SaveChangesAsync();
        f.Access.Select(new() { Key = WorkspaceModes.Platform, Mode = WorkspaceModes.Platform });

        Assert.True((await f.Service.UpdateAdministrator(f.A.Id, f.MemberA.Id,
            new(true, [Permissions.MembersManage]))).IsSuccess);
        var repository = new AccessRepository(f.Context);
        var privateRole = Assert.Single(await repository.MemberRoles(f.A.Id, f.MemberA.Id));
        Assert.Equal("Administrator - Staff Patient (2)", privateRole.Name);
        var count = await f.Context.AccessRoles.CountAsync();

        Assert.True((await f.Service.UpdateAdministrator(f.A.Id, f.MemberA.Id,
            new(false, [Permissions.RolesManage]))).IsSuccess);
        var updatedRole = Assert.Single(await repository.MemberRoles(f.A.Id, f.MemberA.Id));
        Assert.Equal(privateRole.Id, updatedRole.Id);
        Assert.Equal(count, await f.Context.AccessRoles.CountAsync());
        Assert.Equal(new[] { Permissions.RolesManage }, updatedRole.Permissions);
        Assert.Contains(Permissions.ClinicalRecordsRead, f.RoleA.Permissions);
    }

    [Fact]
    public async Task PlatformAdministratorManagementDoesNotChangeAnOrdinaryMember()
    {
        using var f = new Fixture();
        var member = await f.Context.OrganizationMemberships.SingleAsync(row => row.OrganizationId == f.B.Id);
        f.Access.Select(new() { Key = WorkspaceModes.Platform, Mode = WorkspaceModes.Platform });

        var update = await f.Service.UpdateAdministrator(f.B.Id, member.Id, new(false, [Permissions.MembersManage]));
        var remove = await f.Service.RemoveAdministrator(f.B.Id, member.Id);

        Assert.Equal(404, update.Error!.HttpStatus);
        Assert.Equal(404, remove.Error!.HttpStatus);
        Assert.True(member.IsActive);
        Assert.NotNull(await f.Context.OrganizationMemberships.FindAsync(member.Id));
    }

    [Fact]
    public async Task OrganizationAdministratorsCannotUsePlatformManagementEndpoints()
    {
        using var f = new Fixture();
        await f.Select(f.A);
        Assert.Equal(403, (await f.Service.OrganizationAdministrators(f.A.Id)).Error!.HttpStatus);
        Assert.Equal(403, (await f.Service.UpdateAdministrator(f.A.Id, f.MemberA.Id, new(true, Permissions.Administration.ToList()))).Error!.HttpStatus);
        Assert.Equal(403, (await f.Service.RemoveAdministrator(f.A.Id, f.MemberA.Id)).Error!.HttpStatus);
        Assert.Equal(403, (await f.Service.DeleteOrganization(f.A.Id)).Error!.HttpStatus);
    }

    [Fact]
    public async Task PlatformAdministratorEditsRequireTheCorrectOrganizationAndValidGrants()
    {
        using var f = new Fixture();
        f.Access.Select(new() { Key = WorkspaceModes.Platform, Mode = WorkspaceModes.Platform });
        Assert.Equal(404, (await f.Service.RemoveAdministrator(f.B.Id, f.MemberA.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await f.Service.UpdateAdministrator(f.B.Id, f.MemberA.Id, new(true, Permissions.Administration.ToList()))).Error!.HttpStatus);
        Assert.Equal(400, (await f.Service.UpdateAdministrator(f.A.Id, f.MemberA.Id, new(true, [..Permissions.Administration, "platform.admin"]))).Error!.HttpStatus);
        Assert.Equal(400, (await f.Service.UpdateAdministrator(f.A.Id, f.MemberA.Id, new(true, []))).Error!.HttpStatus);
        Assert.True((await f.Service.UpdateAdministrator(f.A.Id, f.MemberA.Id, new(true, [Permissions.MembersManage]))).IsSuccess);
        Assert.Equal(new[] { Permissions.MembersManage }, Assert.Single((await f.Service.OrganizationAdministrators(f.A.Id)).Data!.Administrators).Permissions);
    }

    [Fact]
    public async Task OrganizationsWithPatientOrCareHistoryCannotBeDeleted()
    {
        using var f = new Fixture();
        f.Access.Select(new() { Key = WorkspaceModes.Platform, Mode = WorkspaceModes.Platform });
        var result = await f.Service.DeleteOrganization(f.A.Id);
        Assert.Equal(409, result.Error!.HttpStatus);
        Assert.NotNull(await f.Context.Organizations.FindAsync(f.A.Id));
        Assert.NotEmpty(await f.Context.OrganizationPatients.IgnoreQueryFilters().Where(row => row.OrganizationId == f.A.Id).ToListAsync());
        Assert.NotEmpty(await f.Context.ClinicalRecords.IgnoreQueryFilters().Where(row => row.OrganizationId == f.A.Id).ToListAsync());
    }

    [Fact]
    public async Task UnusedOrganizationDeletionRemovesOnlyItsSetupAndKeepsSharedIdentities()
    {
        using var f = new Fixture();
        var unused = new Organization { Name = "Unused organization", Phone = "300" };
        var member = new OrganizationMembership { OrganizationId = unused.Id, UserId = "staff" };
        var accessRole = new AccessRole { OrganizationId = unused.Id, Name = "Administrator", Permissions = Permissions.Administration.ToList() };
        var location = new Location { OrganizationId = unused.Id, Name = "Main site" };
        var assignment = new PractitionerRole { OrganizationId = unused.Id, PractitionerId = f.AssignmentA.PractitionerId, LocationId = location.Id };
        var schedule = new PractitionerSchedule { OrganizationId = unused.Id, PractitionerRoleId = assignment.Id };
        var slot = new SessionSlot { OrganizationId = unused.Id, SessionId = schedule.Id, Session = schedule, IsAvailable = true };
        f.Context.AddRange(unused, member, accessRole, location, assignment, slot,
            new MembershipAccessRole { OrganizationId = unused.Id, MembershipId = member.Id, AccessRoleId = accessRole.Id });
        await f.Context.SaveChangesAsync();
        f.Context.ChangeTracker.Clear();
        f.Access.Select(new() { Key = WorkspaceModes.Platform, Mode = WorkspaceModes.Platform });
        Assert.True((await f.Service.DeleteOrganization(unused.Id)).IsSuccess);
        Assert.Null(await f.Context.Organizations.FirstOrDefaultAsync(row => row.Id == unused.Id));
        Assert.Empty(await f.Context.OrganizationMemberships.Where(row => row.OrganizationId == unused.Id).ToListAsync());
        Assert.Empty(await f.Context.MembershipAccessRoles.Where(row => row.OrganizationId == unused.Id).ToListAsync());
        Assert.Empty(await f.Context.PractitionerRoles.IgnoreQueryFilters().Where(row => row.OrganizationId == unused.Id).ToListAsync());
        Assert.Empty(await f.Context.SessionSlots.IgnoreQueryFilters().Where(row => row.OrganizationId == unused.Id).ToListAsync());
        Assert.NotNull(await f.Context.Users.FindAsync("staff"));
        Assert.NotNull(await f.Context.Organizations.FindAsync(f.A.Id));
        Assert.Equal(404, (await f.Service.DeleteOrganization(unused.Id)).Error!.HttpStatus);
    }

    [Fact]
    public async Task RevokedMembershipGrantsAssignmentsAndAccountStatusAreRechecked()
    {
        using var f = new Fixture();
        var key = $"{WorkspaceModes.Work}:{f.A.Id}:{f.AssignmentA.Id}";
        Assert.NotNull(await f.Workspaces.Resolve("staff", key));
        var member = await f.Context.OrganizationMemberships.FindAsync(f.MemberA.Id);
        member!.IsActive = false;
        await f.Context.SaveChangesAsync();
        Assert.Null(await f.Workspaces.Resolve("staff", key));
        member.IsActive = true;
        var role = await f.Context.AccessRoles.FindAsync(f.RoleA.Id);
        role!.Permissions = [];
        await f.Context.SaveChangesAsync();
        Assert.Empty((await f.Workspaces.Resolve("staff", key))!.Permissions);
        var assignment = await f.Context.PractitionerRoles.FindAsync(f.AssignmentA.Id);
        assignment!.EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await f.Context.SaveChangesAsync();
        Assert.Null(await f.Workspaces.Resolve("staff", key));
        var user = await f.Context.Users.FindAsync("staff");
        user!.IsActive = false;
        await f.Context.SaveChangesAsync();
        Assert.Empty(await f.Scopes("staff"));
    }

    [Fact]
    public async Task LocationScopeIncludesChildrenAndExcludesOtherLocations()
    {
        using var f = new Fixture();
        var member = await f.Context.OrganizationMemberships.FindAsync(f.MemberA.Id);
        member!.LocationId = f.Root.Id;
        await f.Context.SaveChangesAsync();
        await f.Select(f.A);
        Assert.Contains(f.Child.Id, f.Access.LocationIds);
        Assert.DoesNotContain(f.Elsewhere.Id, f.Access.LocationIds);
        Assert.Null(await f.Context.PractitionerRoles.FirstOrDefaultAsync(row => row.Id == f.OutsideAssignment.Id));
        var denied = await f.Service.UpdateMember(f.MemberA.Id, new(true, [f.RoleA.Id], null));
        Assert.Equal(403, denied.Error!.HttpStatus);
    }

    [Theory]
    [InlineData("", "appointments.manage", false)]
    [InlineData("work:unknown", "appointments.manage", false)]
    [InlineData("b", "clinical-records.read", false)]
    [InlineData("a", "encounters.start", true)]
    [InlineData("a", "appointments.manage", false)]
    [InlineData("a", "schedules.manage,appointments.manage", false)]
    public async Task FilterDeniesInvalidWorkspaceOrMissingPermissionBeforeTheAction(string choice, string permission, bool assignment)
    {
        using var f = new Fixture();
        var key = choice == "a" ? $"{WorkspaceModes.Work}:{f.A.Id}" : choice == "b" ? $"{WorkspaceModes.Work}:{f.B.Id}" : choice;
        var http = new DefaultHttpContext { User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "staff"), new Claim(ClaimTypes.Role, "PlatformAdmin")], "test")) };
        http.Request.Headers["X-Workspace"] = key;
        var descriptor = new ActionDescriptor { EndpointMetadata = [new WorkspaceAttribute(WorkspaceModes.Work, permission, assignment)] };
        var action = new ActionContext(http, new RouteData(), descriptor);
        var executing = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var called = false;
        await new WorkspaceFilter(f.Workspaces, f.Access).OnActionExecutionAsync(executing, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()));
        });
        var expected = choice == "a" && !assignment;
        Assert.Equal(expected, called);
        if (!expected) Assert.Equal(403, Assert.IsType<ObjectResult>(executing.Result).StatusCode);
    }

    [Fact]
    public async Task PlatformAdministrationDoesNotGrantClinicalAccessOrWorkMembership()
    {
        using var f = new Fixture();
        var user = await f.Context.Users.FindAsync("staff");
        user!.IsPlatformAdmin = true;
        await f.Context.SaveChangesAsync();
        f.Access.Select((await f.Scopes("staff")).Single(row => row.Key == WorkspaceModes.Platform));
        Assert.Empty(await f.Context.ClinicalRecords.ToListAsync());
        Assert.Empty(await f.Context.Appointments.ToListAsync());
        Assert.Empty(f.Access.Current!.Permissions);
    }

    [Fact]
    public void BusinessEndpointsHaveWorkspaceRulesAndNoJobRoleAuthorization()
    {
        var exceptions = new HashSet<string> { "UsersController.Login", "UsersController.GetUser", "UsersController.UpdateUser", "WorkspaceController.Get", "WorkspaceController.PermissionList" };
        foreach (var controller in typeof(UsersController).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ControllerBase))))
        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => method.IsDefined(typeof(HttpMethodAttribute))))
        {
            Assert.All(controller.GetCustomAttributes<AuthorizeAttribute>().Concat(method.GetCustomAttributes<AuthorizeAttribute>()), attribute => Assert.Null(attribute.Roles));
            if (!exceptions.Contains(controller.Name + "." + method.Name)) Assert.NotNull(method.GetCustomAttribute<WorkspaceAttribute>());
        }
    }

    [Theory]
    [InlineData(WorkspaceModes.Work)]
    [InlineData(WorkspaceModes.Patient)]
    public void WorkspaceFiltersTranslateToPostgreSql(string mode)
    {
        var access = new WorkspaceAccess();
        access.Select(new() { Key = mode, Mode = mode, OrganizationId = Guid.NewGuid(), PatientId = Guid.NewGuid(), Permissions = Permissions.All.ToList() });
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused").Options, access);
        Assert.Contains("WHERE", context.ClinicalRecords.Include(row => row.Prescriptions).ToQueryString());
        Assert.Contains("WHERE", context.Patients.ToQueryString());
        Assert.Contains("WHERE", context.Appointments.ToQueryString());
        Assert.Contains("WHERE", context.SessionHistories.Include(row => row.PractitionerSchedule).ToQueryString());
    }

    private sealed class ExistingAccount(NexMed360Api.Dtos.User.ViewUserDto user) : NexMed360Api.Interfaces.Services.IAuthService
    {
        public Task<Result<NexMed360Api.Dtos.User.ViewUserDto>> RegisterUser(NexMed360Api.Dtos.User.RegisterUserDto dto, string role) =>
            Task.FromResult<Result<NexMed360Api.Dtos.User.ViewUserDto>>(user);
        public Task<Result<NexMed360Api.Dtos.User.LoginResponseDto>> LoginAsync(NexMed360Api.Dtos.User.LoginUserDto dto) => throw new NotSupportedException();
    }

    private sealed class NoopRegistrationTransaction : NexMed360Api.Interfaces.Services.IRegistrationTransactions, NexMed360Api.Interfaces.Services.IRegistrationTransaction
    {
        public Task<NexMed360Api.Interfaces.Services.IRegistrationTransaction> BeginAsync() => Task.FromResult<NexMed360Api.Interfaces.Services.IRegistrationTransaction>(this);
        public Task CommitAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Fixture : IDisposable
    {
        public WorkspaceAccess Access { get; } = new();
        public ApplicationDbContext Context { get; }
        public AccessService Service { get; }
        public WorkspaceService Workspaces { get; }
        public async Task<List<WorkspaceScope>> Scopes(string userId)
        {
            var rows = new List<WorkspaceScope>();
            foreach (var choice in await Workspaces.List(userId)) rows.Add((await Workspaces.Resolve(userId, choice.Key))!);
            return rows;
        }
        public Organization A { get; } = new() { Name = "Organization A", Phone = "100" };
        public Organization B { get; } = new() { Name = "Organization B", Phone = "200" };
        public Patient Own { get; }
        public Patient Other { get; }
        public Patient Unregistered { get; } = new() { Gender = "Test" };
        public PractitionerRole AssignmentA { get; }
        public PractitionerRole AssignmentB { get; }
        public PractitionerRole OutsideAssignment { get; }
        public OrganizationMembership MemberA { get; }
        public AccessRole RoleA { get; }
        public AccessRole RoleB { get; }
        public ClinicalRecord RecordB { get; }
        public ClinicalRecord OtherRecord { get; }
        public Appointment AppointmentB { get; }
        public SessionSlot SlotA { get; }
        public Location Root { get; }
        public Location Child { get; }
        public Location Elsewhere { get; }

        public Fixture()
        {
            Context = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Access);
            var staff = new ApplicationUser { Id = "staff", FirstName = "Staff", LastName = "Patient", Email = "staff@example.test" };
            staff.PersonId = staff.Person.Id;
            var other = new ApplicationUser { Id = "other", FirstName = "Other", LastName = "Patient", Email = "other@example.test" };
            other.PersonId = other.Person.Id;
            Own = new() { PersonId = staff.PersonId, Gender = "Test" };
            Other = new() { PersonId = other.PersonId, Gender = "Test" };
            var practitioner = new Practitioner { PersonId = staff.PersonId };
            Root = new() { OrganizationId = A.Id, Name = "Clinic" };
            Child = new() { OrganizationId = A.Id, Name = "Room", ParentId = Root.Id };
            Elsewhere = new() { OrganizationId = A.Id, Name = "Other clinic" };
            AssignmentA = new() { OrganizationId = A.Id, PractitionerId = practitioner.Id, LocationId = Child.Id };
            AssignmentB = new() { OrganizationId = B.Id, PractitionerId = practitioner.Id };
            OutsideAssignment = new() { OrganizationId = A.Id, PractitionerId = practitioner.Id, LocationId = Elsewhere.Id };
            MemberA = new() { UserId = "staff", OrganizationId = A.Id };
            var memberB = new OrganizationMembership { UserId = "staff", OrganizationId = B.Id };
            RoleA = new() { OrganizationId = A.Id, Name = "Custom team", Permissions = Permissions.All.ToList() };
            RoleB = new() { OrganizationId = B.Id, Name = "Administrator", Permissions = [Permissions.PatientsRead] };
            Context.AddRange(A, B, staff, other, Own, Other, Unregistered, practitioner, Root, Child, Elsewhere,
                AssignmentA, AssignmentB, OutsideAssignment, MemberA, memberB, RoleA, RoleB,
                new MembershipAccessRole { OrganizationId = A.Id, MembershipId = MemberA.Id, AccessRoleId = RoleA.Id },
                new MembershipAccessRole { OrganizationId = B.Id, MembershipId = memberB.Id, AccessRoleId = RoleB.Id },
                new OrganizationPatient { OrganizationId = A.Id, PatientId = Own.Id },
                new OrganizationPatient { OrganizationId = B.Id, PatientId = Own.Id },
                new OrganizationPatient { OrganizationId = A.Id, PatientId = Other.Id });
            ClinicalRecord Record(Organization organization, PractitionerRole assignment, Patient patient, bool visible = true) =>
                new() { OrganizationId = organization.Id, PractitionerRoleId = assignment.Id, PatientId = patient.Id, Diagnosis = "Synthetic record", IsPatientVisible = visible };
            RecordB = Record(B, AssignmentB, Own);
            OtherRecord = Record(A, AssignmentA, Other);
            var recordA = Record(A, AssignmentA, Own);
            recordA.Prescriptions!.Add(new() { OrganizationId = A.Id, ClinicalRecordId = recordA.Id, MedicineName = "Synthetic medicine", Dosage = "Test" });
            Context.AddRange(recordA, RecordB, OtherRecord, Record(B, AssignmentB, Own, false));
            var scheduleA = new PractitionerSchedule { OrganizationId = A.Id, PractitionerRoleId = AssignmentA.Id };
            var scheduleB = new PractitionerSchedule { OrganizationId = B.Id, PractitionerRoleId = AssignmentB.Id };
            SlotA = new() { OrganizationId = A.Id, SessionId = scheduleA.Id, Session = scheduleA, IsAvailable = true, Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) };
            var slotB = new SessionSlot { OrganizationId = B.Id, SessionId = scheduleB.Id, Session = scheduleB, IsAvailable = true };
            AppointmentB = Appointment.Book(Own.Id, slotB, "staff", null);
            Context.AddRange(SlotA, slotB, Appointment.Book(Own.Id, SlotA, "staff", null), AppointmentB, Appointment.Book(Other.Id, SlotA, "other", null));
            Context.SaveChanges();
            Context.ChangeTracker.Clear();
            Service = new(new AccessRepository(Context), Access);
            Workspaces = new(new WorkspaceRepository(Context));
        }

        public async Task Select(Organization organization, PractitionerRole? assignment = null) => Access.Select(
            (await Scopes("staff")).Single(row => row.Key == $"{WorkspaceModes.Work}:{organization.Id}" + (assignment == null ? "" : $":{assignment.Id}")));
        public void Dispose() => Context.Dispose();
    }
}
