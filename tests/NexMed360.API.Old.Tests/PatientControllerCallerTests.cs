using NexMed360.Application.Access;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Controllers.v1;
using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests;

public class PatientControllerCallerTests
{
    [Theory]
    [InlineData(WorkspaceModes.Patient, false)]
    [InlineData(WorkspaceModes.Work, true)]
    public async Task ReadActionsPassAuthenticatedIdentityAndSelectedMode(string roles, bool expectedIsPractitionerRole)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "signed-in-user") };
        claims.AddRange(roles.Split(',').Select(role => new Claim(ClaimTypes.Role, role)));
        var service = new RecordingPatientService();
        var controller = Controller(service, claims, roles);
        var requestedId = Guid.NewGuid().ToString();

        await controller.GetClinicalRecord(requestedId);

        Assert.Equal(requestedId, service.RequestedId);
        Assert.Equal("signed-in-user", service.RequesterUserId);
        Assert.Equal(expectedIsPractitionerRole, service.IsPractitionerRole);

        await controller.GetClinicalRecords(requestedId);

        Assert.Equal(requestedId, service.RequestedId);
        Assert.Equal("signed-in-user", service.RequesterUserId);
        Assert.Equal(expectedIsPractitionerRole, service.IsPractitionerRole);
    }

    [Fact]
    public async Task ReadActionsDoNotInventMissingCallerIdentity()
    {
        var service = new RecordingPatientService();
        var controller = Controller(service, [new Claim(ClaimTypes.Role, UserRoles.PRACTITIONER)]);

        await controller.GetClinicalRecord(Guid.NewGuid().ToString());
        Assert.Null(service.RequesterUserId);

        await controller.GetClinicalRecords(Guid.NewGuid().ToString());
        Assert.Null(service.RequesterUserId);
    }

    [Theory]
    [InlineData(nameof(PatientController.GetClinicalRecord))]
    [InlineData(nameof(PatientController.GetClinicalRecords))]
    public void ReadActionsRequireWorkspaceAndClinicalPermission(string action)
    {
        var method = typeof(PatientController).GetMethod(action)!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Null(authorization.Roles);
        var workspace = method.GetCustomAttribute<WorkspaceAttribute>()!;
        Assert.Equal(new[] { WorkspaceModes.Work, WorkspaceModes.Patient }, workspace.Modes);
        Assert.Equal(Permissions.ClinicalRecordsRead, workspace.Permission);
    }

    private static PatientController Controller(RecordingPatientService service, IEnumerable<Claim> claims, string mode = WorkspaceModes.Patient) => new(service, Workspace(mode))
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        }
    };

    private static WorkspaceAccess Workspace(string mode)
    {
        var access = new WorkspaceAccess();
        access.Select(new() { Mode = mode });
        return access;
    }

    private sealed class RecordingPatientService : IPatientService
    {
        public Task<Result<ViewPatientDto>> GetOwnPatient() => throw new NotSupportedException();
        public string? RequestedId { get; private set; }
        public string? RequesterUserId { get; private set; }
        public bool IsPractitionerRole { get; private set; }

        public Task<Result<ViewClinicalRecordDto>> ViewClinicalRecord(string id, string? requesterUserId, bool isPractitionerRole)
        {
            (RequestedId, RequesterUserId, IsPractitionerRole) = (id, requesterUserId, isPractitionerRole);
            return Task.FromResult(Result<ViewClinicalRecordDto>.Success(null));
        }

        public Task<Result<List<ViewClinicalRecordDto>>> ViewAllClinicalRecords(string patientId, string? requesterUserId, bool isPractitionerRole)
        {
            (RequestedId, RequesterUserId, IsPractitionerRole) = (patientId, requesterUserId, isPractitionerRole);
            return Task.FromResult(Result<List<ViewClinicalRecordDto>>.Success([]));
        }

        public Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto) => throw new NotSupportedException();
        public Task<Result<ViewPatientDto>> GetPatient(string id) => throw new NotSupportedException();
        public Task<Result<ViewClinicalRecordDto>> AddToClinicalRecords(AddClinicalRecordDto dto, string practitionerRoleUserId) => throw new NotSupportedException();
    }
}
