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
    [InlineData("Patient")]
    [InlineData("Doctor")]
    [InlineData("Receptionist")]
    [InlineData("Patient,Receptionist,SuperUser")]
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
        Assert.Equal(roles.Where(role => role != UserRoles.SUPERUSER).Order(), service.RequesterRoles.Order());
        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, response.StatusCode);
        Assert.Null(Assert.IsType<Result<ViewAppointmentDto>>(response.Value).Data);
    }

    [Fact]
    public async Task ReadDoesNotInferCallerIdentityFromTheRequestedAppointmentId()
    {
        var service = new RecordingAppointmentService();
        var controller = Controller(service, [new Claim("test-role", UserRoles.DOCTOR)]);

        await controller.ViewAppointment(Guid.NewGuid().ToString());

        Assert.Null(service.RequesterId);
        Assert.Equal(new[] { UserRoles.DOCTOR }, service.RequesterRoles);
    }

    [Fact]
    public void ReadKeepsItsExistingPatientDoctorReceptionistAuthorization()
    {
        var method = typeof(AppointmentsController).GetMethod(nameof(AppointmentsController.ViewAppointment))!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(new[] { UserRoles.DOCTOR, UserRoles.PATIENT, UserRoles.RECEPTIONIST },
            authorization.Roles!.Split(',').Select(role => role.Trim()).Order().ToArray());
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
        var claims = new List<Claim> { new("test-role", UserRoles.DOCTOR) };
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
        Assert.Equal(new[] { UserRoles.DOCTOR }, service.RequesterRoles);
        Assert.Equal(404, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData(nameof(AppointmentsController.ConfirmAppointment), "Doctor,Receptionist")]
    [InlineData(nameof(AppointmentsController.CompleteAppointment), "Doctor,Receptionist")]
    [InlineData(nameof(AppointmentsController.ConfirmArrival), "Doctor,Receptionist")]
    [InlineData(nameof(AppointmentsController.ConfirmInProgress), "Doctor")]
    public void StatusActionsKeepExistingRoleRestrictions(string action, string allowedRoles)
    {
        var method = typeof(AppointmentsController).GetMethod(action)!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(allowedRoles.Split(',').Order(),
            authorization.Roles!.Split(',').Select(role => role.Trim()).Order());
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData("Patient", "signed-in-user")]
    [InlineData("Doctor", "signed-in-user")]
    [InlineData("Receptionist", "signed-in-user")]
    [InlineData("Patient,SuperUser", "signed-in-user")]
    [InlineData("Patient", null)]
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
        Assert.Equal(roles.Where(role => role != UserRoles.SUPERUSER).Order(), service.RequesterRoles.Order());
        Assert.Equal(404, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public void CancellationRetainsItsExistingPatientDoctorReceptionistRolesAndPostMethod()
    {
        var method = typeof(AppointmentsController).GetMethod(nameof(AppointmentsController.CancelAppointment))!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(new[] { UserRoles.DOCTOR, UserRoles.PATIENT, UserRoles.RECEPTIONIST },
            authorization.Roles!.Split(',').Select(role => role.Trim()).Order().ToArray());
        Assert.Single(method.GetCustomAttributes<HttpPostAttribute>());
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    private static AppointmentsController Controller(RecordingAppointmentService service, IEnumerable<Claim> claims) => new(service)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, "test-role"))
            }
        }
    };

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
        public Task<Result<List<ViewAppointmentDto>>> GetHospitalAppointments(ViewAppointmentsFilterDto filter, string userId) => throw new NotSupportedException();
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
