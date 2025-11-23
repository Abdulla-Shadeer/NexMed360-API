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

    public AppointmentsController(IAppointmentService appointmentsService)
    {
        _appointmentsService = appointmentsService;
    }

    [HttpPost]
    [Route("Create")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CreateAppointment(CreateAppointmentDto createAppointment)
    {
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.Create(createAppointment, requesterId!), true);
    }

    [HttpGet]
    [Route("ViewOwn")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<List<ViewAppointmentDto>>>> ViewOwnAppointments([FromQuery] ViewAppointmentsFilterDto filter)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.GetOwn(filter, userId));
    }

    [HttpGet]
    [Route("ViewHospital")]
    [Authorize(Roles = UserRoles.RECEPTIONIST)]
    public async Task<ActionResult<Result<List<ViewAppointmentDto>>>> ViewHospitalAppointments([FromQuery] ViewAppointmentsFilterDto filter)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.GetHospitalAppointments(filter, userId));
    }

    [HttpPost]
    [Route("Edit")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto)
    {
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.Modify(modifyAppointmentDto, requesterId!));
    }

    [HttpGet]
    [Route("View/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ViewAppointment(string appointmentId)
    {
        return HandleResult(await _appointmentsService.Get(appointmentId));
    }

    [HttpGet]
    [Route("Confirm/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ConfirmAppointment(string appointmentId)
    {
        return HandleResult(await _appointmentsService.ChangeStatus(appointmentId, AppointmentStatus.Confirmed));
    }

    [HttpGet]
    [Route("Complete/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CompleteAppointment(string appointmentId)
    {
        return HandleResult(await _appointmentsService.ChangeStatus(appointmentId, AppointmentStatus.Completed));
    }

    [HttpGet]
    [Route("ConfirmArrival/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ConfirmArrival(string appointmentId)
    {
        return HandleResult(await _appointmentsService.ChangeStatus(appointmentId, AppointmentStatus.Arrived));
    }

    [HttpGet]
    [Route("ConfirmInProgress/{appointmentId}")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ConfirmInProgress(string appointmentId)
    {
        return HandleResult(await _appointmentsService.ChangeStatus(appointmentId, AppointmentStatus.InProgress));
    }

    [HttpPost]
    [Route("Cancel")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CancelAppointment(CancellationRequestDto cancellationRequestDto)
    {
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.Cancel(cancellationRequestDto, requesterId!));
    }

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreationResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreationResult ? Created(string.Empty, result) : Ok(result);
    }
}
