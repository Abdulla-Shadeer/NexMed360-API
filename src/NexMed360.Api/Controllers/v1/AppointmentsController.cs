using NexMed360.Application.Access;
using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NexMed360Api.Controllers.v1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentsService;
    private readonly WorkspaceAccess _workspace;

    public AppointmentsController(IAppointmentService appointmentsService, WorkspaceAccess? workspace = null)
    {
        _appointmentsService = appointmentsService;
        _workspace = workspace ?? new();
    }

    [HttpPost]
    [Route("Create")]
    [Authorize]
    [Workspace(WorkspaceModes.Patient + "," + WorkspaceModes.Work, Permissions.AppointmentsManage)]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CreateAppointment(CreateAppointmentDto createAppointment)
    {
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.Create(createAppointment, requesterId!), true);
    }

    [HttpGet]
    [Route("ViewOwn")]
    [Authorize]
    [Workspace(WorkspaceModes.Patient + "," + WorkspaceModes.Work, Permissions.AppointmentsManage)]
    public async Task<ActionResult<Result<List<ViewAppointmentDto>>>> ViewOwnAppointments([FromQuery] ViewAppointmentsFilterDto filter)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.GetOwn(filter, userId));
    }

    [HttpGet]
    [Route("ViewOrganization")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.AppointmentsManage)]
    public async Task<ActionResult<Result<List<ViewAppointmentDto>>>> ViewOrganizationAppointments([FromQuery] ViewAppointmentsFilterDto filter)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.GetOrganizationAppointments(filter, userId));
    }

    [HttpGet]
    [Route("View/{appointmentId}")]
    [Authorize]
    [Workspace(WorkspaceModes.Patient + "," + WorkspaceModes.Work, Permissions.AppointmentsManage)]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ViewAppointment(string appointmentId)
    {
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var requesterPermissions = GetRequesterPermissions();
        return HandleResult(await _appointmentsService.Get(appointmentId, requesterId, requesterPermissions));
    }

    [HttpGet]
    [Route("Confirm/{appointmentId}")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.AppointmentsManage)]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ConfirmAppointment(string appointmentId)
    {
        return HandleResult(await ChangeStatusForCurrentUser(appointmentId, AppointmentStatus.Confirmed));
    }

    [HttpGet]
    [Route("Complete/{appointmentId}")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.AppointmentsManage)]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CompleteAppointment(string appointmentId)
    {
        return HandleResult(await ChangeStatusForCurrentUser(appointmentId, AppointmentStatus.Completed));
    }

    [HttpGet]
    [Route("ConfirmArrival/{appointmentId}")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.AppointmentsManage)]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ConfirmArrival(string appointmentId)
    {
        return HandleResult(await ChangeStatusForCurrentUser(appointmentId, AppointmentStatus.Arrived));
    }

    [HttpGet]
    [Route("ConfirmInProgress/{appointmentId}")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.EncountersStart, true)]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ConfirmInProgress(string appointmentId)
    {
        return HandleResult(await ChangeStatusForCurrentUser(appointmentId, AppointmentStatus.InProgress));
    }

    [HttpPost]
    [Route("Cancel")]
    [Authorize]
    [Workspace(WorkspaceModes.Patient + "," + WorkspaceModes.Work, Permissions.AppointmentsManage)]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CancelAppointment(CancellationRequestDto cancellationRequestDto)
    {
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.Cancel(cancellationRequestDto, requesterId, GetRequesterPermissions()));
    }

    private IReadOnlyCollection<string> GetRequesterPermissions() => _workspace.Mode == WorkspaceModes.Patient
        ? [WorkspaceModes.Patient] : _workspace.Current?.Permissions ?? [];

    private Task<Result<ViewAppointmentDto>> ChangeStatusForCurrentUser(string appointmentId, AppointmentStatus status)
        => _appointmentsService.ChangeStatus(appointmentId, status,
            User.FindFirstValue(ClaimTypes.NameIdentifier), GetRequesterPermissions());

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreationResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreationResult ? Created(string.Empty, result) : Ok(result);
    }
}
