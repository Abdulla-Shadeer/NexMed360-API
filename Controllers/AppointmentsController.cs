using FinalProjectApi.Common;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinalProjectApi.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
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
        return HandleResult(await _appointmentsService.CreateAppointment(createAppointment, requesterId!), true);
    }

    [HttpGet]
    [Route("ViewOwn")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<List<ViewAppointmentDto>>>> ViewOwnAppointments([FromQuery] ViewAppointmentsFilterDto filter)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.ViewOwnAppointments(filter, userId));
    }

    [HttpGet]
    [Route("ViewHospital")]
    [Authorize(Roles = UserRoles.RECEPTIONIST)]
    public async Task<ActionResult<Result<List<ViewAppointmentDto>>>> ViewHospitalAppointments([FromQuery] ViewAppointmentsFilterDto filter)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.ViewHospitalAppointments(filter, userId));
    }

    [HttpPost]
    [Route("Edit")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto)
    {
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.ModifyAppointment(modifyAppointmentDto, requesterId!));
    }

    [HttpGet]
    [Route("View/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ViewAppointment(string appointmentId)
    {
        return HandleResult(await _appointmentsService.ViewAppointment(appointmentId));
    }

    [HttpGet]
    [Route("Confirm/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ConfirmAppointment(string appointmentId)
    {
        return HandleResult(await _appointmentsService.ConfirmOrCompleteAppointment(appointmentId, false));
    }

    [HttpGet]
    [Route("Complete/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CompleteAppointment(string appointmentId)
    {
        return HandleResult(await _appointmentsService.ConfirmOrCompleteAppointment(appointmentId, true));
    }

    [HttpPost]
    [Route("Cancel")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CancelAppointment(CancellationRequestDto cancellationRequestDto)
    {
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _appointmentsService.CancelAppointment(cancellationRequestDto, requesterId!));
    }

    [HttpPost]
    [Route("CreateDoctorSession")]
    [Authorize(Roles = $"{UserRoles.HOSPITAL_ADMIN}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewDoctorSessionDto>>> CreateDoctorSession(CreateDoctorSessionDto createSessionDto)
    { 
        return HandleResult(await  _appointmentsService.CreateDoctorSession(createSessionDto), true);
    }

    [HttpGet]
    [Route("GetDoctorSessions/{doctorId}")]
    [Authorize(Roles = $"{UserRoles.HOSPITAL_ADMIN}, {UserRoles.DOCTOR}, {UserRoles.PATIENT}")]
    public async Task<ActionResult<Result<List<ViewDoctorSessionDto>>>> GetDoctorSessions(Guid doctorId)
    {
        return HandleResult(await _appointmentsService.GetDoctorSessions(doctorId));
    }

    [HttpGet]
    [Route("GetSessionSlots")]
    [Authorize(Roles = $"{UserRoles.HOSPITAL_ADMIN}, {UserRoles.DOCTOR}, {UserRoles.PATIENT}")]
    public async Task<ActionResult<Result<List<ViewSessionSlotDto>>>> GetSessionSlots([FromQuery] GetSessionSlotsDto dto) {
        return HandleResult(await _appointmentsService.GetSessionSlots(dto));
    }

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreationResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreationResult ? Created(string.Empty, result) : Ok(result);
    }
}
