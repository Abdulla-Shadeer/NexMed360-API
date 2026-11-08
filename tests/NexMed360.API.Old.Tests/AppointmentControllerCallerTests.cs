using NexMed360.Application.Access;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Controllers.v1;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests;

public class AppointmentControllerCallerTests
{
    [Theory]
    [InlineData(WorkspaceModes.Patient)]
    [InlineData("encounters.start")]
    [InlineData("appointments.manage")]
    [InlineData("patient,appointments.manage,PlatformAdmin")]
    public async Task ReadPassesOnlySupportedTokenRolesAndCallerIdentity(string roleNames)
    {
        var roles = roleNames.Split(',');
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "signed-in-user") };
        // A custom role claim type verifies the controller uses IsInRole.
        claims.AddRange(roles.Select(role => new Claim("test-role", role)));
        var service = new RecordingAppointmentService();
        var controller = Controller(service, claims);
        var id = Guid.NewGuid().ToString();

        var result = await controller.ViewAppointment(id);

        Assert.Equal(id, service.AppointmentId);
        Assert.Equal("signed-in-user", service.RequesterId);
        Assert.Equal(roles.Where(role => role != UserRoles.PLATFORM_ADMIN).Order(), service.RequesterRoles.Order());
        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, response.StatusCode);
        Assert.Null(Assert.IsType<Result<ViewAppointmentDto>>(response.Value).Data);
    }

    [Fact]
    public async Task ReadDoesNotInferCallerIdentityFromTheRequestedAppointmentId()
    {
        var service = new RecordingAppointmentService();
        var controller = Controller(service, [new Claim("test-role", Permissions.EncountersStart)]);

        await controller.ViewAppointment(Guid.NewGuid().ToString());

        Assert.Null(service.RequesterId);
        Assert.Equal(new[] { Permissions.EncountersStart }, service.RequesterRoles);
    }

    [Fact]
    public void ReadRequiresPatientModeOrAppointmentPermission()
    {
        var method = typeof(AppointmentsController).GetMethod(nameof(AppointmentsController.ViewAppointment))!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());

        Assert.Null(authorization.Roles);
        var workspace = method.GetCustomAttribute<WorkspaceAttribute>()!;
        Assert.Equal(new[] { WorkspaceModes.Patient, WorkspaceModes.Work }, workspace.Modes);
        Assert.Equal(Permissions.AppointmentsManage, workspace.Permission);
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed, "signed-in-user")]
    [InlineData(AppointmentStatus.Completed, "signed-in-user")]
    [InlineData(AppointmentStatus.Arrived, "signed-in-user")]
    [InlineData(AppointmentStatus.InProgress, "signed-in-user")]
    [InlineData(AppointmentStatus.Confirmed, null)]
    [InlineData(AppointmentStatus.Completed, null)]
    [InlineData(AppointmentStatus.Arrived, null)]
    [InlineData(AppointmentStatus.InProgress, null)]
    public async Task StatusActionsPassCallerIdentityRolesAndServerSelectedStatus(AppointmentStatus status, string? caller)
    {
        var service = new RecordingAppointmentService();
        var claims = new List<Claim> { new("test-role", Permissions.EncountersStart) };
        if (caller != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, caller));
        var controller = Controller(service, claims);
        var id = Guid.NewGuid().ToString();

        var result = await (status switch
        {
            AppointmentStatus.Confirmed => controller.ConfirmAppointment(id),
            AppointmentStatus.Completed => controller.CompleteAppointment(id),
            AppointmentStatus.Arrived => controller.ConfirmArrival(id),
            AppointmentStatus.InProgress => controller.ConfirmInProgress(id),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        });

        Assert.Equal(id, service.AppointmentId);
        Assert.Equal(status, service.RequestedStatus);
        Assert.Equal(caller, service.RequesterId);
        Assert.Equal(new[] { Permissions.EncountersStart }, service.RequesterRoles);
        Assert.Equal(404, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData(nameof(AppointmentsController.ConfirmAppointment), "encounters.start,appointments.manage")]
    [InlineData(nameof(AppointmentsController.CompleteAppointment), "encounters.start,appointments.manage")]
    [InlineData(nameof(AppointmentsController.ConfirmArrival), "encounters.start,appointments.manage")]
    [InlineData(nameof(AppointmentsController.ConfirmInProgress), "encounters.start")]
    public void StatusActionsRequireCurrentWorkspacePermissions(string action, string allowedRoles)
    {
        var method = typeof(AppointmentsController).GetMethod(action)!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());

        Assert.Null(authorization.Roles);
        var workspace = method.GetCustomAttribute<WorkspaceAttribute>()!;
        Assert.Equal(new[] { WorkspaceModes.Work }, workspace.Modes);
        Assert.Equal(action == nameof(AppointmentsController.ConfirmInProgress) ? Permissions.EncountersStart : Permissions.AppointmentsManage, workspace.Permission);
        Assert.Equal(action == nameof(AppointmentsController.ConfirmInProgress), workspace.Assignment);
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(WorkspaceModes.Patient, "signed-in-user")]
    [InlineData("encounters.start", "signed-in-user")]
    [InlineData("appointments.manage", "signed-in-user")]
    [InlineData("patient,PlatformAdmin", "signed-in-user")]
    [InlineData(WorkspaceModes.Patient, null)]
    public async Task CancellationPassesTokenIdentityAndRolesSeparatelyFromTheRequestedAppointment(string roleNames, string? caller)
    {
        var service = new RecordingAppointmentService();
        var roles = roleNames.Split(',');
        var claims = roles.Select(role => new Claim("test-role", role)).ToList();
        if (caller != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, caller));
        var controller = Controller(service, claims);
        var dto = new CancellationRequestDto { AppointmentId = Guid.NewGuid().ToString(), Reason = "Synthetic reason" };

        var result = await controller.CancelAppointment(dto);

        Assert.Equal(dto.AppointmentId, service.AppointmentId);
        Assert.Equal(caller, service.RequesterId);
        Assert.Equal(roles.Where(role => role != UserRoles.PLATFORM_ADMIN).Order(), service.RequesterRoles.Order());
        Assert.Equal(404, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public void CancellationRequiresWorkspacePermissionAndPostMethod()
    {
        var method = typeof(AppointmentsController).GetMethod(nameof(AppointmentsController.CancelAppointment))!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Null(authorization.Roles);
        var workspace = method.GetCustomAttribute<WorkspaceAttribute>()!;
        Assert.Equal(new[] { WorkspaceModes.Patient, WorkspaceModes.Work }, workspace.Modes);
        Assert.Equal(Permissions.AppointmentsManage, workspace.Permission);
        Assert.Single(method.GetCustomAttributes<HttpPostAttribute>());
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    private static AppointmentsController Controller(RecordingAppointmentService service, IEnumerable<Claim> claims) => new(service, Workspace(claims))
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, "test-role"))
            }
        }
    };

    private static WorkspaceAccess Workspace(IEnumerable<Claim> claims)
    {
        var access = new WorkspaceAccess();
        var permissions = claims.Where(claim => claim.Type == "test-role" && claim.Value != UserRoles.PLATFORM_ADMIN).Select(claim => claim.Value).ToList();
        access.Select(new() { Mode = WorkspaceModes.Work, Permissions = permissions });
        return access;
    }

    private sealed class RecordingAppointmentService : IAppointmentService
    {
        public string? AppointmentId { get; private set; }
        public string? RequesterId { get; private set; }
        public IReadOnlyCollection<string> RequesterRoles { get; private set; } = [];
        public AppointmentStatus? RequestedStatus { get; private set; }

        public Task<Result<ViewAppointmentDto>> Get(string appointmentId, string? requesterId, IReadOnlyCollection<string> requesterRoles)
        {
            (AppointmentId, RequesterId, RequesterRoles) = (appointmentId, requesterId, requesterRoles);
            return Task.FromResult(Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound));
        }

        public Task<Result<ViewAppointmentDto>> Create(CreateAppointmentDto dto, string requesterId) => throw new NotSupportedException();
        public Task<Result<List<ViewAppointmentDto>>> GetOwn(ViewAppointmentsFilterDto filter, string userId) => throw new NotSupportedException();
        public Task<Result<List<ViewAppointmentDto>>> GetOrganizationAppointments(ViewAppointmentsFilterDto filter, string userId) => throw new NotSupportedException();
        public Task<Result<ViewAppointmentDto>> ChangeStatus(
            string appointmentId, AppointmentStatus status, string? requesterId, IReadOnlyCollection<string> requesterRoles)
        {
            (AppointmentId, RequestedStatus, RequesterId, RequesterRoles) = (appointmentId, status, requesterId, requesterRoles);
            return Task.FromResult(Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound));
        }
        public Task<Result<ViewAppointmentDto>> Cancel(
            CancellationRequestDto dto, string? requesterId, IReadOnlyCollection<string> requesterRoles)
        {
            (AppointmentId, RequesterId, RequesterRoles) = (dto.AppointmentId, requesterId, requesterRoles);
            return Task.FromResult(Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound));
        }
    }
}
